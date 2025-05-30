# Lab 2: ECS + S3 Deployment - Containerized Backend + Static Frontend

## Overview

**Objective**: Deploy the .NET Lecture Summarizer using a modern containerized architecture with AWS ECS for the backend API and S3 + CloudFront for the Blazor WebAssembly frontend.

**Architecture**:
```
Internet → CloudFront → S3 (Blazor SPA)
        ↓
       ALB → ECS Fargate (API Container) → AWS Bedrock
```

**Learning Objectives:**
- Container orchestration with AWS ECS
- Static website hosting with S3 and CloudFront
- Cross-origin resource sharing (CORS) configuration
- Container deployment strategies
- Modern web application architecture patterns

## Prerequisites

- Completed main lab setup (LectureSummarizer solution)
- **Docker installed and running locally** - [Install Docker Desktop](https://docs.docker.com/get-docker/)
- Basic understanding of containers
- Optionally: Resources from Lab 1 (IAM role, security groups can be reused)

### AWS Credentials Setup

⚠️ **Security Best Practice**: Create a dedicated IAM user for this lab instead of using Administrator access.

#### Step 1: Create IAM User (AWS Console)

1. **Navigate to IAM Console** → **Users** → **Create user**

2. **User Details**:
   - **User name**: `lecture-lab-deployer`
   - **Provide user access to AWS Management Console**: Unchecked (API access only)

3. **Set Permissions**:
   - **Attach policies directly**
   - **Add the following managed policies**:
     - `AmazonECS_FullAccess`
     - `AmazonEC2ContainerRegistryFullAccess`
     - `AmazonS3FullAccess`
     - `EC2FullAccess` (for ALB and security groups)
     - `CloudWatchFullAccess`
     - `CloudFrontFullAccess`
   - **Note**: These are broader permissions for lab convenience. In production, use more restrictive policies.

4. **Create User**

#### Step 2: Create Access Keys

1. **Select your user** → **Security credentials** tab

2. **Create access key**:
   - **Use case**: Command Line Interface (CLI)
   - **Confirmation**: Check the box
   - **Description tag**: `Lab2-ECS-Deployment` (optional)

3. **Save Credentials**:
   - **Access key ID**: Save this value
   - **Secret access key**: Save this value
   - **Download .csv file** for backup

#### Step 3: Configure AWS CLI Locally

```bash
# Configure AWS CLI with your lab user credentials
aws configure

# Enter your values:
# AWS Access Key ID: [Your Access Key ID]
# AWS Secret Access Key: [Your Secret Access Key]  
# Default region name: us-east-1
# Default output format: json

# Verify configuration
aws sts get-caller-identity

# Expected output should show your lab user ARN
# {
#     "UserId": "AIDACKCEVSQ6C2EXAMPLE",
#     "Account": "123456789012",
#     "Arn": "arn:aws:iam::123456789012:user/lecture-lab-deployer"
# }
```

#### Step 4: Verify Docker Installation

```bash
# Check Docker is installed and running
docker --version
docker info

# Expected: Docker version information and running status
# If Docker is not running, start Docker Desktop

# Test Docker functionality
docker run hello-world
```

## Step 1: Prepare Applications for Container + Static Deployment

### 1.1 Create Dockerfile for API

📁 **Create in**: `LectureSummarizer.API/` directory

**LectureSummarizer.API/Dockerfile**
```dockerfile
# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

# Use the .NET SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["LectureSummarizer.API/LectureSummarizer.API.csproj", "LectureSummarizer.API/"]
COPY ["LectureSummarizer.Shared/LectureSummarizer.Shared.csproj", "LectureSummarizer.Shared/"]
RUN dotnet restore "LectureSummarizer.API/LectureSummarizer.API.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/LectureSummarizer.API"
RUN dotnet build "LectureSummarizer.API.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "LectureSummarizer.API.csproj" -c Release -o /app/publish --no-restore

# Final stage - runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "LectureSummarizer.API.dll"]
```

### 1.2 Create .dockerignore

📁 **Create in**: `LectureSummarizer/` root directory

**LectureSummarizer/.dockerignore**
```
**/.dockerignore
**/.env
**/.git
**/.gitignore
**/.project
**/.settings
**/.toolstarget
**/.vs
**/.vscode
**/.idea
**/*.*proj.user
**/*.dbmdl
**/*.jfm
**/azds.yaml
**/bin
**/charts
**/docker-compose*
**/Dockerfile*
**/node_modules
**/npm-debug.log
**/obj
**/secrets.dev.yaml
**/values.dev.yaml
LICENSE
README.md
**/.terraform
**/terraform.tfstate*
```

### 1.3 Update API Configuration for Container

**LectureSummarizer.API/appsettings.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Amazon": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AWS": {
    "Region": "us-east-1"
  }
}
```

**LectureSummarizer.API/Program.cs** - Update CORS for CloudFront:
```csharp
using Amazon.BedrockRuntime;
using LectureSummarizer.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AWS services
builder.Services.AddAWSService<IAmazonBedrockRuntime>();

// Add custom services
builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IBedrockService, BedrockService>();

// Add CORS for CloudFront and S3
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:5001",
                "https://localhost:5001",
                "https://*.cloudfront.net",
                "https://*.amazonaws.com"
            )
            .SetIsOriginAllowedToReturnTrue() // Allow any HTTPS origin for development
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWebApp");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    service = "lecture-summarizer-api",
    environment = app.Environment.EnvironmentName
});

app.Run();
```

### 1.4 Update Blazor SPA Configuration

**LectureSummarizer.Web.SPA/wwwroot/appsettings.json**
```json
{
  "ApiBaseUrl": "https://lecture-api-alb-123456789.us-east-1.elb.amazonaws.com"
}
```

**LectureSummarizer.Web.SPA/wwwroot/appsettings.Development.json**
```json
{
  "ApiBaseUrl": "http://localhost:5273"
}
```

### 1.5 Create Build and Deployment Scripts

📁 **Create in**: `LectureSummarizer/scripts/` directory

**scripts/build-container.sh**
```bash
#!/bin/bash

echo "🐳 Building Docker container for API..."

# Set variables
IMAGE_NAME="lecture-summarizer-api"
TAG="latest"
REGION="us-east-1"
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_REPOSITORY="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/${IMAGE_NAME}"

echo "📋 Build Configuration:"
echo "   Image: ${IMAGE_NAME}:${TAG}"
echo "   ECR Repository: ${ECR_REPOSITORY}"
echo "   Region: ${REGION}"

# Build Docker image
echo "🔨 Building Docker image..."
docker build -t ${IMAGE_NAME}:${TAG} -f LectureSummarizer.API/Dockerfile .

if [ $? -eq 0 ]; then
    echo "✅ Docker image built successfully: ${IMAGE_NAME}:${TAG}"
    
    # Tag for ECR
    docker tag ${IMAGE_NAME}:${TAG} ${ECR_REPOSITORY}:${TAG}
    echo "🏷️ Tagged for ECR: ${ECR_REPOSITORY}:${TAG}"
    
    echo "📋 Next steps:"
    echo "   1. Create ECR repository: aws ecr create-repository --repository-name ${IMAGE_NAME}"
    echo "   2. Push to ECR: ./scripts/push-to-ecr.sh"
    echo "   3. Deploy to ECS: ./scripts/deploy-ecs.sh"
else
    echo "❌ Docker build failed"
    exit 1
fi
```

**scripts/push-to-ecr.sh**
```bash
#!/bin/bash

echo "📤 Pushing Docker image to ECR..."

# Set variables
IMAGE_NAME="lecture-summarizer-api"
TAG="latest"
REGION="us-east-1"
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_REPOSITORY="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/${IMAGE_NAME}"

# Login to ECR
echo "🔐 Logging in to ECR..."
aws ecr get-login-password --region ${REGION} | docker login --username AWS --password-stdin ${ECR_REPOSITORY}

# Create ECR repository if it doesn't exist
echo "🏗️ Creating ECR repository (if needed)..."
aws ecr create-repository --repository-name ${IMAGE_NAME} --region ${REGION} 2>/dev/null || echo "Repository already exists"

# Push image
echo "⬆️ Pushing image to ECR..."
docker push ${ECR_REPOSITORY}:${TAG}

if [ $? -eq 0 ]; then
    echo "✅ Image pushed successfully to: ${ECR_REPOSITORY}:${TAG}"
    echo "📋 Image URI: ${ECR_REPOSITORY}:${TAG}"
else
    echo "❌ Push failed"
    exit 1
fi
```

**scripts/build-spa.sh**
```bash
#!/bin/bash

echo "🌐 Building Blazor SPA for S3 deployment..."

# Clean previous builds
rm -rf ./publish/spa
rm -f spa-deployment.zip

# Build and publish the SPA
dotnet publish LectureSummarizer.Web.SPA -c Release -o ./publish/spa

if [ $? -eq 0 ]; then
    echo "✅ SPA built successfully"
    
    # Create deployment package
    cd ./publish/spa/wwwroot
    zip -r ../../../spa-deployment.zip .
    cd ../../../
    
    echo "📦 Deployment package created: spa-deployment.zip"
    echo "📋 Contents: $(unzip -l spa-deployment.zip | grep -E '\.(html|js|css|wasm)$' | wc -l) web files"
    echo "📋 Size: $(du -h spa-deployment.zip | cut -f1)"
    
    echo "📋 Next steps:"
    echo "   1. Create S3 bucket for hosting"
    echo "   2. Upload files: aws s3 sync ./publish/spa/wwwroot s3://your-bucket-name"
    echo "   3. Configure CloudFront distribution"
else
    echo "❌ SPA build failed"
    exit 1
fi
```

**scripts/deploy-ecs.sh**
```bash
#!/bin/bash

echo "🚀 Deploying to ECS..."

# Set variables
CLUSTER_NAME="lecture-summarizer-cluster"
SERVICE_NAME="lecture-summarizer-api-service"
TASK_FAMILY="lecture-summarizer-api-task"
REGION="us-east-1"
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
IMAGE_URI="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/lecture-summarizer-api:latest"

echo "📋 Deployment Configuration:"
echo "   Cluster: ${CLUSTER_NAME}"
echo "   Service: ${SERVICE_NAME}"
echo "   Task Definition: ${TASK_FAMILY}"
echo "   Image: ${IMAGE_URI}"

# Register new task definition
echo "📝 Registering task definition..."
TASK_DEFINITION=$(aws ecs register-task-definition \
    --family ${TASK_FAMILY} \
    --network-mode awsvpc \
    --requires-compatibility FARGATE \
    --cpu 256 \
    --memory 512 \
    --execution-role-arn arn:aws:iam::${ACCOUNT_ID}:role/ecsTaskExecutionRole \
    --task-role-arn arn:aws:iam::${ACCOUNT_ID}:role/LectureSummarizerEC2Role \
    --container-definitions "[{
        \"name\": \"lecture-api\",
        \"image\": \"${IMAGE_URI}\",
        \"portMappings\": [{
            \"containerPort\": 5000,
            \"protocol\": \"tcp\"
        }],
        \"essential\": true,
        \"logConfiguration\": {
            \"logDriver\": \"awslogs\",
            \"options\": {
                \"awslogs-group\": \"/ecs/lecture-summarizer-api\",
                \"awslogs-region\": \"${REGION}\",
                \"awslogs-stream-prefix\": \"ecs\"
            }
        },
        \"environment\": [{
            \"name\": \"ASPNETCORE_ENVIRONMENT\",
            \"value\": \"Production\"
        }, {
            \"name\": \"ASPNETCORE_URLS\",
            \"value\": \"http://+:5000\"
        }]
    }]" \
    --query 'taskDefinition.taskDefinitionArn' \
    --output text)

echo "✅ Task definition registered: ${TASK_DEFINITION}"

# Update service
echo "🔄 Updating ECS service..."
aws ecs update-service \
    --cluster ${CLUSTER_NAME} \
    --service ${SERVICE_NAME} \
    --task-definition ${TASK_DEFINITION} \
    --force-new-deployment

echo "✅ Service update initiated"
echo "📋 Monitor deployment: aws ecs describe-services --cluster ${CLUSTER_NAME} --services ${SERVICE_NAME}"
```

## Step 2: Create ECS Infrastructure (AWS Console)

### 2.1 Create ECS Cluster

1. **Navigate to ECS Console**:
   - Go to AWS Console → Services → Elastic Container Service

2. **Create Cluster**:
   - Click **"Create cluster"**
   - **Cluster name**: `lecture-summarizer-cluster`
   - **Infrastructure**: AWS Fargate (serverless)
   - Click **"Create"**

### 2.2 Create CloudWatch Log Group

1. **Navigate to CloudWatch Console**:
   - Go to AWS Console → Services → CloudWatch

2. **Create Log Group**:
   - Go to **Logs** → **Log groups** → **Create log group**
   - **Log group name**: `/ecs/lecture-summarizer-api`
   - **Retention setting**: 7 days (for cost optimization)
   - Click **"Create"**

### 2.3 Create or Reuse IAM Roles

**Option A: Reuse from Lab 1**
If you completed Lab 1, you can reuse `LectureSummarizerEC2Role` for the task role.

**Option B: Create ECS-specific Roles**

1. **Create ECS Task Execution Role** (if not exists):
   - Go to **IAM Console** → **Roles** → **Create role**
   - **Trusted entity**: AWS service → Elastic Container Service → Elastic Container Service Task
   - **Permissions**: `AmazonECSTaskExecutionRolePolicy`
   - **Role name**: `ecsTaskExecutionRole`

2. **ECS Task Role** (for Bedrock access):
   - Use the `LectureSummarizerEC2Role` from Lab 1, or
   - Create new role with Bedrock permissions (same policy as Lab 1)

### 2.4 Create Application Load Balancer for ECS

1. **Navigate to EC2 Console** → **Load Balancers** → **Create Load Balancer**

2. **Configure ALB**:
   - **Type**: Application Load Balancer
   - **Name**: `lecture-ecs-alb`
   - **Scheme**: Internet-facing
   - **IP address type**: IPv4

3. **Network Mapping**:
   - **VPC**: Default VPC (or your custom VPC)
   - **Mappings**: Select 2+ Availability Zones

4. **Security Groups**:
   - **Create new security group**: `lecture-ecs-alb-sg`
   - **Inbound rules**:
     - HTTP (80) from 0.0.0.0/0
     - HTTPS (443) from 0.0.0.0/0 (if using SSL)

5. **Listeners and Routing**:
   - **Create target group**:
     - **Target group name**: `lecture-ecs-targets`
     - **Target type**: IP addresses
     - **Protocol**: HTTP
     - **Port**: 5000
     - **VPC**: Same as ALB
     - **Health check path**: `/health`
   - **Do not register targets yet** (ECS will do this automatically)

6. **Create Load Balancer**

### 2.5 Create ECS Security Group

1. **Create Security Group**:
   - **Name**: `lecture-ecs-sg`
   - **Description**: `Security group for ECS tasks`
   - **VPC**: Same as ALB

2. **Inbound Rules**:
   - **Type**: Custom TCP
   - **Port**: 5000
   - **Source**: `lecture-ecs-alb-sg` (ALB security group)

3. **Outbound Rules**:
   - **Type**: All traffic
   - **Destination**: 0.0.0.0/0 (for AWS API calls)

## Step 3: Build and Push Container Image

### 3.1 Build Docker Image Locally

```bash
# Navigate to solution root
cd LectureSummarizer

# Make scripts executable
chmod +x scripts/*.sh

# Build Docker image
./scripts/build-container.sh
```

### 3.2 Test Container Locally (Optional)

```bash
# Run container locally for testing
docker run -p 5000:5000 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e AWS_ACCESS_KEY_ID=your_access_key \
  -e AWS_SECRET_ACCESS_KEY=your_secret_key \
  -e AWS_DEFAULT_REGION=us-east-1 \
  lecture-summarizer-api:latest

# Test health endpoint
curl http://localhost:5000/health
```

### 3.3 Push to ECR

```bash
# Push image to ECR
./scripts/push-to-ecr.sh
```

## Step 4: Create ECS Service

### 4.1 Create Task Definition (AWS Console)

1. **Navigate to ECS Console** → **Task definitions** → **Create new task definition**

2. **Configure Task Definition**:
   - **Task definition family**: `lecture-summarizer-api-task`
   - **Launch type**: AWS Fargate
   - **Operating system/Architecture**: Linux/X86_64
   - **CPU**: 0.25 vCPU
   - **Memory**: 0.5 GB
   - **Task execution role**: `ecsTaskExecutionRole`
   - **Task role**: `LectureSummarizerEC2Role`

3. **Container Definition**:
   - **Container name**: `lecture-api`
   - **Image URI**: `[ACCOUNT-ID].dkr.ecr.us-east-1.amazonaws.com/lecture-summarizer-api:latest`
   - **Port mappings**: 
     - **Container port**: 5000
     - **Protocol**: TCP
   - **Environment variables**:
     - `ASPNETCORE_ENVIRONMENT`: `Production`
     - `ASPNETCORE_URLS`: `http://+:5000`

4. **Logging**:
   - **Log driver**: `awslogs`
   - **Log group**: `/ecs/lecture-summarizer-api`
   - **Log region**: `us-east-1`
   - **Log stream prefix**: `ecs`

5. **Create Task Definition**

### 4.2 Create ECS Service (AWS Console)

1. **In ECS Console** → **Clusters** → `lecture-summarizer-cluster` → **Create service**

2. **Configure Service**:
   - **Launch type**: Fargate
   - **Task definition family**: `lecture-summarizer-api-task`
   - **Service name**: `lecture-summarizer-api-service`
   - **Number of tasks**: 1

3. **Network Configuration**:
   - **VPC**: Default VPC (or your custom VPC)
   - **Subnets**: Select public subnets
   - **Security groups**: `lecture-ecs-sg`
   - **Auto-assign public IP**: ENABLED

4. **Load Balancer**:
   - **Load balancer type**: Application Load Balancer
   - **Load balancer**: `lecture-ecs-alb`
   - **Container to load balance**: `lecture-api:5000`
   - **Target group**: `lecture-ecs-targets`

5. **Service auto scaling**: Disabled (for simplicity)

6. **Create Service**

### 4.3 Verify ECS Deployment

1. **Monitor Service**:
   - Go to **Services** tab in your cluster
   - Wait for **Running count**: 1
   - Check **Task** status: RUNNING

2. **Test API**:
   ```bash
   # Get ALB DNS name from EC2 Console → Load Balancers
   curl http://lecture-ecs-alb-123456789.us-east-1.elb.amazonaws.com/health
   ```

## Step 5: Deploy Blazor SPA to S3

### 5.1 Create S3 Bucket for Static Website

1. **Navigate to S3 Console** → **Create bucket**

2. **Configure Bucket**:
   - **Bucket name**: `lecture-summarizer-spa-[random-suffix]` (must be globally unique)
   - **Region**: us-east-1
   - **Block all public access**: UNCHECKED ⚠️
   - **Acknowledge public access warning**: Checked

3. **Create Bucket**

### 5.2 Configure Bucket for Static Website Hosting

1. **Select your bucket** → **Properties** tab

2. **Static website hosting**:
   - **Enable** static website hosting
   - **Index document**: `index.html`
   - **Error document**: `index.html` (for SPA routing)

3. **Save changes**

### 5.3 Set Bucket Policy for Public Read

1. **Permissions** tab → **Bucket policy**

2. **Add policy** (replace `YOUR-BUCKET-NAME`):
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "PublicReadGetObject",
            "Effect": "Allow",
            "Principal": "*",
            "Action": "s3:GetObject",
            "Resource": "arn:aws:s3:::YOUR-BUCKET-NAME/*"
        }
    ]
}
```

### 5.4 Build and Upload SPA

```bash
# Update API URL in SPA configuration
# Edit LectureSummarizer.Web.SPA/wwwroot/appsettings.json
# Set ApiBaseUrl to your ECS ALB DNS name

# Build SPA
./scripts/build-spa.sh

# Upload to S3 (replace with your bucket name)
aws s3 sync ./publish/spa/wwwroot s3://lecture-summarizer-spa-[random-suffix] --delete

# Verify upload
aws s3 ls s3://lecture-summarizer-spa-[random-suffix] --recursive
```

### 5.5 Test S3 Website

```bash
# Get website URL from S3 Console → Properties → Static website hosting
# Visit: http://lecture-summarizer-spa-[random-suffix].s3-website-us-east-1.amazonaws.com
```

## Step 6: Create CloudFront Distribution (Optional but Recommended)

### 6.1 Create CloudFront Distribution

1. **Navigate to CloudFront Console** → **Create distribution**

2. **Origin Settings**:
   - **Origin domain**: Select your S3 bucket from dropdown
   - **S3 bucket access**: Don't use OAI (use bucket policy)

3. **Default Cache Behavior**:
   - **Viewer protocol policy**: Redirect HTTP to HTTPS
   - **Allowed HTTP methods**: GET, HEAD, OPTIONS, PUT, POST, PATCH, DELETE
   - **Cache policy**: Managed-CachingDisabled (for dynamic SPA)

4. **Settings**:
   - **Default root object**: `index.html`

5. **Create Distribution**

6. **Wait for deployment** (Status: Deployed)

### 6.2 Update SPA Configuration for CloudFront

```bash
# Update API CORS to include CloudFront domain
# Get CloudFront distribution domain name
# Update LectureSummarizer.API CORS settings to include CloudFront URL

# Rebuild and redeploy container if needed
./scripts/build-container.sh
./scripts/push-to-ecr.sh
# Update ECS service to use new image
```

## Step 7: Testing and Verification

### 7.1 End-to-End Testing

1. **Access Frontend**: Visit CloudFront URL or S3 website URL
2. **Upload Test PDF**: Use a sample lecture PDF
3. **Verify Summary**: Ensure AI summarization works
4. **Check Cross-Origin**: Verify SPA can call ECS API

### 7.2 Monitoring

**ECS Monitoring**:
1. **ECS Console** → **Clusters** → **Services** → **Metrics**
2. **CloudWatch** → **Log groups** → `/ecs/lecture-summarizer-api`

**S3 Monitoring**:
1. **S3 Console** → **Metrics** tab
2. **CloudWatch** → **S3** metrics

**Application Logs**:
```bash
# View ECS logs
aws logs tail /ecs/lecture-summarizer-api --follow
```

## Step 8: Cleanup Resources (Optional)

💡 **Note**: This cleanup is **optional**. You may want to keep resources for further testing.

### 8.1 Delete ECS Resources
1. **ECS Service**: Delete service (will stop tasks)
2. **Task Definition**: Deregister task definition
3. **ECS Cluster**: Delete cluster

### 8.2 Delete Load Balancer and Target Groups
1. **Load Balancer**: Delete `lecture-ecs-alb`
2. **Target Groups**: Delete `lecture-ecs-targets`

### 8.3 Delete S3 and CloudFront
1. **CloudFront**: Delete distribution (wait for deletion)
2. **S3**: Empty and delete bucket

### 8.4 Delete Container Images
1. **ECR**: Delete repository and images

### 8.5 Delete Security Groups and Logs
1. **Security Groups**: Delete `lecture-ecs-sg` and `lecture-ecs-alb-sg`
2. **CloudWatch**: Delete log group `/ecs/lecture-summarizer-api`

### 8.6 Cleanup IAM User (Optional)
If you no longer need the lab deployer user:
1. **IAM Console** → **Users** → `lecture-lab-deployer`
2. **Security credentials** → **Delete access keys**
3. **Delete user**

## Summary

This lab demonstrated:

✅ **Container Orchestration**: ECS Fargate for serverless containers  
✅ **Static Site Hosting**: S3 + CloudFront for global CDN  
✅ **Modern Architecture**: Separated concerns with independent scaling  
✅ **Security**: Proper IAM roles and security groups  
✅ **Monitoring**: CloudWatch logs and metrics  
✅ **Production Ready**: Health checks, logging, and CORS configuration  

**Key Benefits of This Architecture**:
- 🚀 **Scalable**: ECS auto-scaling and global CDN
- 💰 **Cost-effective**: Pay-per-use serverless containers
- 🔒 **Secure**: Network isolation and IAM-based access
- 🌍 **Global**: CloudFront edge locations worldwide
- 🛠️ **Maintainable**: Container-based deployments

**Next Step**: Proceed to Lab 3 (Lambda + S3) for serverless architecture!