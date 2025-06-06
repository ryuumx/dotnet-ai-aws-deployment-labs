# Lab 1: EC2 Split Deployment - Razor Frontend + API Backend

## Overview

**Objective**: Deploy a .NET Lecture Summarizer application using separate EC2 instances for frontend and backend, with Application Load Balancers (ALB) for traffic distribution.

**Architecture**:
```
Internet → ALB (Frontend) → EC2 (Razor MVC)
        ↓
       ALB (Backend) → EC2 (API) → AWS Bedrock
```

**Learning Objectives:**
- Service separation and inter-service communication
- Application Load Balancer configuration
- EC2 instance management and systemd services
- Security group setup and network isolation
- Environment configuration for production

## Prerequisites

- AWS CLI configured with appropriate permissions
- EC2 Key Pair created
- Basic understanding of Linux commands
- .NET application from main lab (LectureSummarizer solution)

## Step 1: Prepare Applications for EC2 Deployment

### 1.1 Create Production Configuration Files

**LectureSummarizer.API/appsettings.Production.json**
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
    "Region": "us-west-2"
  }
}
```

**LectureSummarizer.Web/appsettings.Production.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApiBaseUrl": "http://lecture-api-alb-123456789.us-west-2.elb.amazonaws.com"
}
```

### 1.2 Update API CORS Configuration

**LectureSummarizer.API/Program.cs** - Update CORS policy:
```csharp
// Add CORS - Updated for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:5235", 
                "https://localhost:7185",
                "http://lecture-web-alb-123456789.us-west-2.elb.amazonaws.com"  // Frontend ALB
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});
```

### 1.3 Create Health Check Endpoint

**LectureSummarizer.API/Controllers/HealthController.cs**
```csharp
using Microsoft.AspNetCore.Mvc;

namespace LectureSummarizer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                service = "lecture-summarizer-api"
            });
        }
    }
}
```

### 1.4 Create Deployment Scripts

📁 **Important**: Create these scripts in your **root solution directory** (`LectureSummarizer/`)

Your folder structure should look like:
```
LectureSummarizer/
├── scripts/                        # ← Create this folder
│   ├── deploy-api.sh               # ← Create these scripts here
│   ├── deploy-web.sh
│   └── ec2-setup.sh
├── LectureSummarizer.API/
├── LectureSummarizer.Web/
├── LectureSummarizer.Web.SPA/
├── LectureSummarizer.Shared/
└── LectureSummarizer.sln
```

**scripts/deploy-api.sh**
```bash
#!/bin/bash

# API Deployment Script for EC2
echo "🚀 Deploying API to EC2..."

# Clean previous builds
rm -rf ./publish/api
rm -f api-deployment.tar.gz

# Build and publish the API
dotnet publish LectureSummarizer.API -c Release -o ./publish/api --self-contained false

# Create deployment package
cd ./publish/api
tar -czf ../../api-deployment.tar.gz .
cd ../../

echo "✅ API deployment package created: api-deployment.tar.gz"
echo "📦 Size: $(du -h api-deployment.tar.gz | cut -f1)"
echo "📋 Next steps:"
echo "   1. Upload to EC2: scp -i ~/.ssh/lecture-summarizer-key.pem api-deployment.tar.gz ec2-user@[API-EC2-IP]:/tmp/"
echo "   2. SSH and deploy: ssh -i ~/.ssh/lecture-summarizer-key.pem ec2-user@[API-EC2-IP]"
```

**scripts/deploy-web.sh**
```bash
#!/bin/bash

# Web Deployment Script for EC2
echo "🚀 Deploying Web App to EC2..."

# Clean previous builds
rm -rf ./publish/web
rm -f web-deployment.tar.gz

# Build and publish the web app
dotnet publish LectureSummarizer.Web -c Release -o ./publish/web --self-contained false

# Create deployment package
cd ./publish/web
tar -czf ../../web-deployment.tar.gz .
cd ../../

echo "✅ Web deployment package created: web-deployment.tar.gz"
echo "📦 Size: $(du -h web-deployment.tar.gz | cut -f1)"
echo "📋 Next steps:"
echo "   1. Upload to EC2: scp -i ~/.ssh/lecture-summarizer-key.pem web-deployment.tar.gz ec2-user@[WEB-EC2-IP]:/tmp/"
echo "   2. SSH and deploy: ssh -i ~/.ssh/lecture-summarizer-key.pem ec2-user@[WEB-EC2-IP]"
```

**scripts/ec2-setup.sh**
```bash
#!/bin/bash

# EC2 Instance Setup Script (run on Amazon Linux 2023)
echo "⚙️ Setting up EC2 instance for .NET application..."

# Update system
sudo dnf update -y

# Install .NET 9 Runtime
sudo dnf install -y dotnet-runtime-9.0 aspnetcore-runtime-9.0

# Create application directory
sudo mkdir -p /var/www/app
sudo chown ec2-user:ec2-user /var/www/app

# Install nginx for reverse proxy (optional)
sudo dnf install -y nginx

# Configure firewall
sudo firewall-cmd --permanent --add-port=5000/tcp
sudo firewall-cmd --reload

echo "✅ EC2 setup complete!"
echo "📋 Next steps:"
echo "   1. Deploy your application to /var/www/app/"
echo "   2. Create and start systemd service"
echo "   3. Configure nginx if needed"
```

💡 **To create the scripts**:
1. Open terminal in your `LectureSummarizer/` directory
2. Run: `mkdir scripts`
3. Create each script file with the content above
4. Make them executable: `chmod +x scripts/*.sh`

## Step 2: AWS Infrastructure Setup

### 2.1 Create VPC and Subnets (AWS Console)

**Option A: Use Default VPC (Recommended for Lab)**
- For simplicity, you can use the default VPC that comes with every AWS account
- Skip to Step 2.2 if using default VPC

**Option B: Create Custom VPC (Advanced)**

1. **Navigate to VPC Console**:
   - Go to AWS Console → Services → VPC

2. **Create VPC**:
   - Click **"Create VPC"**
   - Choose **"VPC only"**
   - **Name tag**: `lecture-summarizer-vpc`
   - **IPv4 CIDR block**: `10.0.0.0/16`
   - **IPv6 CIDR block**: No IPv6 CIDR block
   - **Tenancy**: Default
   - Click **"Create VPC"**

3. **Create Internet Gateway**:
   - Go to **Internet Gateways** → **Create Internet Gateway**
   - **Name tag**: `lecture-igw`
   - Click **"Create internet gateway"**
   - **Attach to VPC**: Select your VPC and attach

4. **Create Public Subnets**:
   - Go to **Subnets** → **Create subnet**
   - **VPC ID**: Select your VPC
   - **Subnet settings**:
     - **Subnet name**: `lecture-public-1a`
     - **Availability Zone**: us-west-2a
     - **IPv4 CIDR block**: `10.0.1.0/24`
   - Add another subnet:
     - **Subnet name**: `lecture-public-1b`
     - **Availability Zone**: us-west-2b
     - **IPv4 CIDR block**: `10.0.2.0/24`
   - Click **"Create subnet"**

5. **Create Route Table**:
   - Go to **Route Tables** → **Create route table**
   - **Name**: `lecture-public-rt`
   - **VPC**: Select your VPC
   - Click **"Create route table"**
   - **Edit routes** → **Add route**:
     - **Destination**: `0.0.0.0/0`
     - **Target**: Internet Gateway (select your IGW)
   - **Subnet associations**: Associate both public subnets

### 2.2 Create Key Pair for SSH Access

⚠️ **IMPORTANT**: You'll need this key pair to access your EC2 instances

1. **Navigate to EC2 Console**:
   - Go to AWS Console → Services → EC2

2. **Create Key Pair**:
   - In the left sidebar, click **"Key Pairs"**
   - Click **"Create key pair"**
   - **Name**: `lecture-summarizer-key`
   - **Key pair type**: RSA
   - **Private key file format**: .pem
   - Click **"Create key pair"**

3. **Save the Key File**:
   - The file `lecture-summarizer-key.pem` will automatically download
   - **Move it to a secure location** (e.g., `~/.ssh/`)
   - **Set proper permissions**:
     ```bash
     chmod 400 ~/.ssh/lecture-summarizer-key.pem
     ```
   - **Remember this location** - you'll need it for SSH access!

### 2.3 Create Security Groups (AWS Console)

1. **Navigate to Security Groups**:
   - In EC2 Console → **Security Groups** → **Create security group**

#### ALB Security Group

2. **Create ALB Security Group**:
   - **Security group name**: `lecture-alb-sg`
   - **Description**: `Security group for Lecture Summarizer ALBs`
   - **VPC**: Select your VPC (or default VPC)
   
   **Inbound rules**:
   - Click **"Add rule"**
     - **Type**: HTTP
     - **Protocol**: TCP
     - **Port range**: 80
     - **Source**: 0.0.0.0/0 (Anywhere IPv4)
     - **Description**: Allow HTTP from internet
   
   - Click **"Add rule"** (if using HTTPS)
     - **Type**: HTTPS
     - **Protocol**: TCP
     - **Port range**: 443
     - **Source**: 0.0.0.0/0 (Anywhere IPv4)
     - **Description**: Allow HTTPS from internet
   
   - Click **"Create security group"**

#### API Security Group

3. **Create API Security Group**:
   - **Security group name**: `lecture-api-sg`
   - **Description**: `Security group for Lecture Summarizer API`
   - **VPC**: Same VPC as ALB
   
   **Inbound rules**:
   - **Rule 1**:
     - **Type**: Custom TCP
     - **Protocol**: TCP
     - **Port range**: 5000
     - **Source**: Select "Custom" → Choose `lecture-alb-sg`
     - **Description**: Allow HTTP from ALB
   
   - **Rule 2**:
     - **Type**: SSH
     - **Protocol**: TCP
     - **Port range**: 22
     - **Source**: 0.0.0.0/0 (or your IP for better security)
     - **Description**: SSH access
   
   - Click **"Create security group"**

#### Frontend Security Group

4. **Create Frontend Security Group**:
   - **Security group name**: `lecture-web-sg`
   - **Description**: `Security group for Lecture Summarizer Web`
   - **VPC**: Same VPC as ALB
   
   **Inbound rules**:
   - **Rule 1**:
     - **Type**: Custom TCP
     - **Protocol**: TCP
     - **Port range**: 5000
     - **Source**: Select "Custom" → Choose `lecture-alb-sg`
     - **Description**: Allow HTTP from ALB
   
   - **Rule 2**:
     - **Type**: SSH
     - **Protocol**: TCP
     - **Port range**: 22
     - **Source**: 0.0.0.0/0 (or your IP for better security)
     - **Description**: SSH access
   
   - Click **"Create security group"**

### 2.4 Create IAM Role for EC2 Bedrock Access (AWS Console)

1. **Navigate to IAM Console**:
   - Go to AWS Console → Services → IAM

2. **Create IAM Role**:
   - Click **"Roles"** → **"Create role"**
   - **Trusted entity type**: AWS service
   - **Use case**: EC2
   - Click **"Next"**

3. **Create Custom Policy for Bedrock**:
   - Click **"Create policy"** (opens new tab)
   - Click **"JSON"** tab
   - Replace content with:
   ```json
   {
       "Version": "2012-10-17",
       "Statement": [
           {
               "Effect": "Allow",
               "Action": [
                   "bedrock:InvokeModel",
                   "bedrock:InvokeModelWithResponseStream"
               ],
               "Resource": "arn:aws:bedrock:*::foundation-model/anthropic.claude-3-5-sonnet-20241022-v2:0"
           }
       ]
   }
   ```
   - Click **"Next"**
   - **Policy name**: `BedrockInvokePolicy`
   - **Description**: `Allow invoking Bedrock Claude models`
   - Click **"Create policy"**

4. **Attach Policy to Role**:
   - Return to the role creation tab
   - Search for `BedrockInvokePolicy`
   - Check the box next to it
   - Click **"Next"**

5. **Name and Create Role**:
   - **Role name**: `LectureSummarizerEC2Role`
   - **Description**: `EC2 role for Lecture Summarizer with Bedrock access`
   - Click **"Create role"**

### 2.5 Launch EC2 Instances (AWS Console)

#### Launch API Instance

1. **Navigate to EC2 Dashboard**:
   - Go to EC2 Console → **"Launch instance"**

2. **Configure API Instance**:
   - **Name**: `lecture-api-server`
   
   **Application and OS Images**:
   - **AMI**: Amazon Linux 2023 AMI (should be first option)
   
   **Instance type**:
   - **Instance type**: t3.micro (or t2.micro for free tier)
   
   **Key pair**:
   - **Key pair name**: `lecture-summarizer-key` (the one you created earlier)
   
   **Network settings**:
   - **VPC**: Select your VPC (or default)
   - **Subnet**: Select a public subnet
   - **Auto-assign public IP**: Enable
   - **Firewall (security groups)**: Select existing security group
   - **Security groups**: `lecture-api-sg`
   
   **Configure storage**:
   - **Size**: 8 GiB (default is fine)
   
   **Advanced details**:
   - **IAM instance profile**: `LectureSummarizerEC2Role`
   - **User data**: Copy and paste the content from `scripts/ec2-setup.sh`

3. **Launch Instance**:
   - Review settings
   - Click **"Launch instance"**

#### Launch Frontend Instance

4. **Launch Second Instance**:
   - Click **"Launch instance"** again
   
   **Configure Frontend Instance**:
   - **Name**: `lecture-web-server`
   - **AMI**: Amazon Linux 2023 AMI (same as API)
   - **Instance type**: t3.micro
   - **Key pair**: `lecture-summarizer-key`
   
   **Network settings**:
   - **VPC**: Same VPC
   - **Subnet**: Different public subnet (for high availability)
   - **Auto-assign public IP**: Enable
   - **Security groups**: `lecture-web-sg`
   
   **Advanced details**:
   - **IAM instance profile**: Leave blank (frontend doesn't need Bedrock access)
   - **User data**: Same content from `scripts/ec2-setup.sh`

5. **Launch Instance**:
   - Click **"Launch instance"**

#### Verify Instances

6. **Check Instance Status**:
   - Go to **EC2 Dashboard** → **Instances**
   - Wait for both instances to show:
     - **Instance State**: Running
     - **Status Check**: 2/2 checks passed
   - **Note down the Public IP addresses** - you'll need them for deployment!

## Step 3: Configure Application Load Balancers (AWS Console)

### 3.1 Create API Load Balancer

1. **Navigate to EC2 Console** → Load Balancers → **Create Load Balancer**

2. **Choose Application Load Balancer**

3. **Basic Configuration:**
   - **Name**: `lecture-api-alb`
   - **Scheme**: Internet-facing
   - **IP address type**: IPv4

4. **Network Mapping:**
   - **VPC**: Select your VPC (or default)
   - **Mappings**: Select 2+ Availability Zones
   - Choose public subnets

5. **Security Groups:**
   - Select: `lecture-alb-sg`

6. **Listeners and Routing:**
   - **Create Target Group**:
     - **Target group name**: `lecture-api-targets`
     - **Target type**: Instances
     - **Protocol**: HTTP
     - **Port**: 5000
     - **VPC**: Same as ALB
   - **Health Checks**:
     - **Health check path**: `/api/health`
     - **Healthy threshold**: 2
     - **Unhealthy threshold**: 5
     - **Timeout**: 5 seconds
     - **Interval**: 30 seconds

7. **Register Targets:**
   - Select your API EC2 instance
   - **Port**: 5000

8. **Review and Create**

### 3.2 Create Frontend Load Balancer

Follow similar steps for frontend:

1. **Basic Configuration:**
   - **Name**: `lecture-web-alb`

2. **Target Group:**
   - **Target group name**: `lecture-web-targets`
   - **Port**: 5000
   - **Health check path**: `/` (default MVC route)

3. **Register Targets:**
   - Select your Frontend EC2 instance

## Step 4: Deploy Applications to EC2

### 4.1 Deploy API Backend

```bash
# 1. Build deployment package locally
chmod +x scripts/deploy-api.sh
./scripts/deploy-api.sh

# 2. Get API EC2 instance public IP from AWS Console
# Go to EC2 Console → Instances → Select "lecture-api-server" → Copy Public IPv4 address

# 3. Upload deployment package (replace [API-EC2-IP] with actual IP)
scp -i ~/.ssh/lecture-summarizer-key.pem api-deployment.tar.gz ec2-user@[API-EC2-IP]:/tmp/

# 4. SSH to API server and deploy
ssh -i ~/.ssh/lecture-summarizer-key.pem ec2-user@[API-EC2-IP]
```

**On API EC2 instance:**
```bash
# Extract application
cd /var/www/app
sudo tar -xzf /tmp/api-deployment.tar.gz
sudo chown -R ec2-user:ec2-user /var/www/app

# Create systemd service
sudo tee /etc/systemd/system/lecture-api.service > /dev/null <<EOF
[Unit]
Description=Lecture Summarizer API
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /var/www/app/LectureSummarizer.API.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=lecture-api
User=ec2-user
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
WorkingDirectory=/var/www/app

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable lecture-api
sudo systemctl start lecture-api

# Check status
sudo systemctl status lecture-api
sudo journalctl -u lecture-api -f
```

### 4.2 Deploy Frontend

```bash
# 1. Update configuration with API ALB DNS name
# First, get your API ALB DNS name from AWS Console:
# Go to EC2 Console → Load Balancers → Select "lecture-api-alb" → Copy DNS name

# Edit LectureSummarizer.Web/appsettings.Production.json
# Set ApiBaseUrl to: http://[YOUR-API-ALB-DNS-NAME]

# 2. Build deployment package
chmod +x scripts/deploy-web.sh
./scripts/deploy-web.sh

# 3. Get Frontend EC2 instance public IP from AWS Console
# Go to EC2 Console → Instances → Select "lecture-web-server" → Copy Public IPv4 address

# 4. Upload deployment package (replace [WEB-EC2-IP] with actual IP)
scp -i ~/.ssh/lecture-summarizer-key.pem web-deployment.tar.gz ec2-user@[WEB-EC2-IP]:/tmp/

# 5. SSH to web server and deploy
ssh -i ~/.ssh/lecture-summarizer-key.pem ec2-user@[WEB-EC2-IP]
```

**On Frontend EC2 instance:**
```bash
# Extract application
cd /var/www/app
sudo tar -xzf /tmp/web-deployment.tar.gz
sudo chown -R ec2-user:ec2-user /var/www/app

# Create systemd service
sudo tee /etc/systemd/system/lecture-web.service > /dev/null <<EOF
[Unit]
Description=Lecture Summarizer Web
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /var/www/app/LectureSummarizer.Web.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=lecture-web
User=ec2-user
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
WorkingDirectory=/var/www/app

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable lecture-web
sudo systemctl start lecture-web

# Check status
sudo systemctl status lecture-web
sudo journalctl -u lecture-web -f
```

## Step 5: Update CORS Configuration

### 5.1 Get ALB DNS Names (AWS Console)

1. **Get Frontend ALB DNS name**:
   - Go to **EC2 Console** → **Load Balancers**
   - Select **"lecture-web-alb"**
   - Copy the **DNS name** (e.g., `lecture-web-alb-123456789.us-west-2.elb.amazonaws.com`)

2. **Get API ALB DNS name**:
   - Select **"lecture-api-alb"**
   - Copy the **DNS name** (e.g., `lecture-api-alb-987654321.us-west-2.elb.amazonaws.com`)

### 5.2 Update API CORS Settings

Update the API's `appsettings.Production.json` and redeploy:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AWS": {
    "Region": "us-west-2"
  },
  "CorsOrigins": [
    "http://lecture-web-alb-123456789.us-west-2.elb.amazonaws.com"
  ]
}
```

Update `Program.cs` to read from configuration:
```csharp
var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? new string[0];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            policy.WithOrigins(corsOrigins.Concat(new[] {
                "http://localhost:5235", 
                "https://localhost:7185"
            }).ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});
```

## Step 6: Testing and Verification

### 6.1 Health Checks

```bash
# Test API health endpoint
curl http://lecture-api-alb-123456789.us-west-2.elb.amazonaws.com/api/health

# Expected response:
# {"status":"healthy","timestamp":"2024-01-15T10:30:00Z","service":"lecture-summarizer-api"}
```

### 6.2 End-to-End Testing

1. **Access Frontend**: Visit frontend ALB DNS name in browser
2. **Upload Test PDF**: Use a sample lecture PDF
3. **Verify Summary**: Ensure AI summarization works
4. **Check Logs**: Monitor application logs on both instances

### 6.3 Monitoring Commands

```bash
# Check application status on EC2 (SSH to your instances)
ssh -i ~/.ssh/lecture-summarizer-key.pem ec2-user@[EC2-IP]

sudo systemctl status lecture-api
sudo systemctl status lecture-web

# View real-time logs
sudo journalctl -u lecture-api -f
sudo journalctl -u lecture-web -f
```

**Check Target Group Health (AWS Console)**:
1. Go to **EC2 Console** → **Target Groups**
2. Select **"lecture-api-targets"** or **"lecture-web-targets"**
3. Click **"Targets"** tab
4. Verify instances show **"healthy"** status

**Monitor ALB Metrics (AWS Console)**:
1. Go to **EC2 Console** → **Load Balancers**
2. Select your ALB
3. Click **"Monitoring"** tab
4. View metrics like Request Count, Response Time, etc.

## Step 7: Cleanup Resources (Optional)

💡 **Note**: This cleanup step is **optional**. You may want to keep your resources running for further testing or to proceed to other labs. However, be aware that keeping EC2 instances and load balancers running will incur AWS charges.

When you're completely done with the lab and want to clean up resources to avoid charges:

### 7.1 Terminate EC2 Instances
1. **Go to EC2 Console** → **Instances**
2. **Select both instances** (`lecture-api-server` and `lecture-web-server`)
3. **Instance State** → **Terminate instance**
4. **Confirm termination**

### 7.2 Delete Load Balancers
1. **Go to EC2 Console** → **Load Balancers**
2. **Select** `lecture-api-alb`
3. **Actions** → **Delete load balancer**
4. **Type "confirm"** and delete
5. **Repeat for** `lecture-web-alb`

### 7.3 Delete Target Groups
1. **Go to EC2 Console** → **Target Groups**
2. **Select** `lecture-api-targets`
3. **Actions** → **Delete**
4. **Repeat for** `lecture-web-targets`

### 7.4 Delete Security Groups
1. **Go to EC2 Console** → **Security Groups**
2. **Select** `lecture-api-sg`
3. **Actions** → **Delete security group**
4. **Repeat for** `lecture-web-sg` and `lecture-alb-sg`

### 7.5 Delete IAM Resources
1. **Go to IAM Console** → **Roles**
2. **Select** `LectureSummarizerEC2Role`
3. **Delete role** (it will automatically detach policies)
4. **Go to Policies**
5. **Select** `BedrockInvokePolicy`
6. **Actions** → **Delete policy**

### 7.6 Delete Key Pair (Optional)
1. **Go to EC2 Console** → **Key Pairs**
2. **Select** `lecture-summarizer-key`
3. **Actions** → **Delete**
4. **Also delete the .pem file** from your local machine

### 7.7 Delete Custom VPC (If Created)
If you created a custom VPC:
1. **Go to VPC Console**
2. **Delete subnets first**
3. **Detach and delete internet gateway**
4. **Delete route tables**
5. **Finally delete the VPC**

⚠️ **Cost Consideration**: If you plan to continue with other labs (Lab 2: ECS+S3 or Lab 3: Lambda+S3), you can **reuse some resources** like the IAM role, security groups, and VPC to save time and reduce costs.

## Summary

This lab demonstrated:

✅ **Service Separation**: Frontend and backend on separate EC2 instances  
✅ **Load Balancing**: ALB for high availability and scalability  
✅ **Security**: Proper security groups and IAM roles  
✅ **Monitoring**: Health checks and logging  
✅ **Production Deployment**: Systemd services and proper configuration

**Next Steps**: Proceed to Lab 2 (ECS + S3) or Lab 3 (Lambda + S3) to explore containerized and serverless architectures.
