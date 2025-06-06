# .NET Application Deployment on AWS
## Three Architecture Patterns: Traditional, Modern, and Serverless

This repository contains a comprehensive lab series demonstrating three different AWS deployment architectures for the same .NET application. You'll learn how to deploy a real-world application using traditional EC2 instances, modern containerization, and cutting-edge serverless technologies.

## The Application: Lecture Summarizer

### Overview

The **Lecture Summarizer** is a practical .NET application that demonstrates AI integration with AWS cloud services. It accepts PDF lecture uploads and generates intelligent summaries using AWS Bedrock (Claude 3 Sonnet).

### Architecture Components

**Backend Technologies:**
- **.NET 9 Web API** - RESTful API for PDF processing and AI integration
- **AWS Bedrock** - AI-powered visual analysis using Claude 3.5 Sonnet v2
- **Docnet.Core** - PDF to image conversion library for visual processing
- **CORS Configuration** - Cross-origin support for frontend communication

**Frontend Options:**
- **ASP.NET Core MVC (Razor)** - Server-side rendering for traditional deployments
- **Blazor WebAssembly** - Client-side SPA for modern/serverless deployments

**Key Features:**
- 📄 **PDF Upload** - Accepts lecture documents in PDF format
- 🖼️ **Visual AI Analysis** - Converts PDF pages to images for comprehensive visual analysis
- 🤖 **AI Summarization** - Uses Claude 3.5 Sonnet v2 to analyze diagrams, charts, and formatted content
- 🎨 **Responsive UI** - Clean, Bootstrap-styled interface
- 🔒 **Secure Processing** - Proper error handling and validation
- 📊 **Health Monitoring** - Built-in health check endpoints

### Frontend/Backend Architecture Flexibility

**🔄 Mix and Match Capability**: This application demonstrates **separation of concerns** between frontend and backend, allowing you to deploy different combinations across the labs:

**Backend Options:**
- **EC2 API** (Lab 1) - Traditional server deployment
- **ECS Container** (Lab 2) - Containerized API
- **Lambda Function** (Lab 3) - Serverless API

**Frontend Options:**
- **Razor MVC on EC2** (Lab 1) - Server-side rendering
- **Blazor SPA on S3** (Labs 2 & 3) - Client-side application
                                                                                                                            
**Possible Combinations:**
```
Frontend              Backend              Use Case
─────────────────────────────────────────────────────────────
Razor MVC (EC2)    ←→ API (EC2)          Traditional monolithic
Razor MVC (EC2)    ←→ API (ECS)          Hybrid: traditional UI + modern backend
Blazor SPA (S3)    ←→ API (EC2)          Modern UI + traditional backend  
Blazor SPA (S3)    ←→ API (ECS)          Full modern architecture
Blazor SPA (S3)    ←→ API (Lambda)       Full serverless architecture
```

**🎯 Learning Value**: This flexibility teaches you how **microservices** and **API-first design** enable architectural choices based on specific requirements like cost, scale, and team expertise.

### Project Structure

```
LectureSummarizer/
├── LectureSummarizer.API/              # Backend Web API
│   ├── Controllers/                    # API controllers
│   ├── Services/                       # Business logic (PDF, Bedrock)
│   └── Program.cs                      # API configuration
├── LectureSummarizer.Web/              # Razor MVC Frontend
│   ├── Controllers/                    # MVC controllers
│   ├── Views/                          # Razor views
│   └── Program.cs                      # Web app configuration
├── LectureSummarizer.Web.SPA/          # Blazor WebAssembly Frontend
│   ├── Pages/                          # Blazor components
│   ├── Services/                       # API integration
│   └── Program.cs                      # SPA configuration
├── LectureSummarizer.Shared/           # Shared models and DTOs
├── scripts/                            # Deployment and build scripts
└── LectureSummarizer.sln               # Solution file

# Additional projects created during labs:
# LectureSummarizer.Lambda/             # Created in Lab 3 for serverless deployment
# template.yaml                         # SAM template created in Lab 3
```

## Lab Series Overview

This lab series teaches three fundamental cloud deployment patterns by deploying the same application using different AWS architectures. Each lab builds upon cloud computing concepts while demonstrating distinct approaches to scalability, cost optimization, and operational complexity.

### Lab 1: EC2 Split Deployment - Traditional Infrastructure 🖥️

**Architecture Pattern**: Traditional server-based deployment with service separation

```
Internet → ALB (Frontend) → EC2 (Razor MVC)
        ↓
       ALB (Backend) → EC2 (.NET API) → AWS Bedrock
```

**Key Learning Objectives:**
- **Infrastructure Fundamentals** - VPC, Security Groups, IAM roles
- **Service Separation** - Independent frontend and backend instances
- **Load Balancing** - Application Load Balancer configuration
- **Production Deployment** - Systemd services and monitoring

**When to Use:**
- Legacy applications requiring specific OS configurations
- Applications needing persistent local storage
- Environments requiring full server control
- Traditional enterprise environments

---

### Lab 2: ECS + S3 Deployment - Modern Containerization 🐳

**Architecture Pattern**: Containerized backend with static frontend hosting

```
Internet → CloudFront → S3 (Blazor SPA)
        ↓
       ALB → ECS Fargate (API Container) → AWS Bedrock
```

**Key Learning Objectives:**
- **Container Orchestration** - ECS Fargate for serverless containers
- **Static Site Hosting** - S3 + CloudFront for global performance
- **Modern CI/CD** - Docker builds and ECR registry
- **Scalable Architecture** - Auto-scaling containers and CDN

**When to Use:**
- Microservices architectures
- Applications with predictable traffic patterns
- Teams adopting containerization
- Modern web applications requiring global reach

---

### Lab 3: Lambda + S3 Deployment - Serverless Computing ⚡

**Architecture Pattern**: Fully serverless with Infrastructure as Code

```
Internet → CloudFront → S3 (Blazor SPA)
        ↓
       API Gateway → Lambda (.NET) → AWS Bedrock
```

**Key Learning Objectives:**
- **Serverless Computing** - AWS Lambda with .NET Native AOT
- **Infrastructure as Code** - AWS SAM templates and CloudFormation
- **Event-Driven Architecture** - API Gateway integration
- **Cost Optimization** - Pay-per-request pricing model

**When to Use:**
- Variable or unpredictable traffic
- Cost-sensitive applications
- Event-driven workloads
- Startups and rapid prototyping

## Architecture Comparison Summary

### Infrastructure Management

| Aspect | Lab 1 (EC2) | Lab 2 (ECS) | Lab 3 (Lambda) |
|--------|-------------|-------------|----------------|
| **Infrastructure** | Manual EC2 management | Container orchestration | Fully serverless |
| **Scaling** | Manual/Auto Scaling Groups | ECS auto-scaling | Automatic |
| **Maintenance** | OS updates required | Container updates | Zero maintenance |
| **Cold Start** | No cold start | Minimal | Potential cold start |

### Cost and Operations

| Aspect | Lab 1 (EC2) | Lab 2 (ECS) | Lab 3 (Lambda) |
|--------|-------------|-------------|----------------|
| **Cost Model** | Always-on instances | Container runtime | Pay-per-request |
| **Deployment** | Direct file deployment | Container images | SAM/CloudFormation |
| **Monitoring** | CloudWatch + manual setup | Container insights | Built-in Lambda metrics |
| **Security** | Manual security groups | Container + IAM roles | Managed IAM integration |

### Development Experience

| Aspect | Lab 1 (EC2) | Lab 2 (ECS) | Lab 3 (Lambda) |
|--------|-------------|-------------|----------------|
| **Local Development** | Run directly | Docker required | SAM CLI + Docker |
| **Debugging** | Standard debugging | Container debugging | Lambda local testing |
| **Deployment Speed** | Manual steps | Container build + deploy | Infrastructure as Code |
| **Rollback** | Manual process | ECS rolling updates | CloudFormation rollback |

## Decision Framework

### Choose **Lab 1 (EC2)** when you need:
- ✅ Full operating system control
- ✅ Legacy application compatibility
- ✅ Persistent local storage requirements
- ✅ Predictable, always-on workloads
- ✅ Specific software or configuration needs

### Choose **Lab 2 (ECS)** when you want:
- ✅ Modern containerized architecture
- ✅ Microservices deployment patterns
- ✅ Better resource utilization than EC2
- ✅ Container-based CI/CD pipelines
- ✅ Balanced control and managed services

### Choose **Lab 3 (Lambda)** when you prioritize:
- ✅ Zero server management
- ✅ Automatic scaling to zero
- ✅ Pay-per-request cost model
- ✅ Event-driven architecture
- ✅ Rapid development and deployment

## Getting Started

### Prerequisites

- **AWS Account** with appropriate permissions
- **.NET 9 SDK** installed locally
- **Docker Desktop** (for Labs 2 & 3)
- **AWS CLI** installed and configured
- **AWS SAM CLI** (for Lab 3)
- **Git** for version control

### 🔧 AWS CLI Installation & Configuration

**Required for all labs**: The AWS CLI is essential for managing AWS resources and deploying applications.

**Step 1: Install AWS CLI**

**Windows:**
```bash
# Download and run the AWS CLI MSI installer
# Visit: https://awscli.amazonaws.com/AWSCLIV2.msi
# Or use winget:
winget install Amazon.AWSCLI
```

**macOS:**
```bash
# Using Homebrew (recommended):
brew install awscli

# Or download the installer:
curl "https://awscli.amazonaws.com/AWSCLIV2.pkg" -o "AWSCLIV2.pkg"
sudo installer -pkg AWSCLIV2.pkg -target /
```

**Linux:**
```bash
# Download and install:
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# For Ubuntu/Debian, you can also use:
sudo apt update && sudo apt install awscli
```

**Verification:**
```bash
# Verify installation
aws --version
# Should output something like: aws-cli/2.x.x Python/3.x.x
```

**Step 2: Create IAM User and Access Keys**

1. **Create IAM User for Development**:
   - Go to **IAM Console** → **Users** → **Create user**
   - **User name**: `lecture-developer`
   - **Attach policies directly**:
     - `AmazonBedrockFullAccess` (for AI functionality)
     - `CloudWatchFullAccess` (for logging)
     - `AmazonEC2FullAccess` (for Lab 1 - EC2 instances, VPC, security groups)
     - `AmazonECS_FullAccess` (for Lab 2 - container orchestration)
     - `AmazonS3FullAccess` (for Labs 2 & 3 - static hosting, file storage)
     - `AWSLambda_FullAccess` (for Lab 3 - serverless functions)
     - `CloudFrontFullAccess` (for Labs 2 & 3 - CDN)
     - `ElasticLoadBalancingFullAccess` (for Labs 1 & 2 - load balancers)
     - `IAMFullAccess` (for creating service roles across all labs)
     - `AmazonAPIGatewayAdministrator` (for Lab 3 - API Gateway)
     - `AWSCloudFormationFullAccess` (for Lab 3 - Infrastructure as Code)

   **⚠️ Security Note**: These are broad permissions for learning purposes. In production environments, use more restrictive, least-privilege policies.

2. **Create Access Keys**:
   - Select your user → **Security credentials** tab
   - **Create access key** → **Command Line Interface (CLI)**
   - **Save credentials** securely

**Step 3: Configure AWS CLI**

```bash
# Configure AWS credentials
aws configure

# Enter your values:
# AWS Access Key ID: [Your Access Key ID from Step 2]
# AWS Secret Access Key: [Your Secret Access Key from Step 2]
# Default region name: us-west-2
# Default output format: json

# Verify configuration
aws sts get-caller-identity
```

### ⚠️ Critical Setup: AWS Bedrock Model Access

**REQUIRED FOR ALL LABS**: Before proceeding with any lab (local testing, EC2, ECS, or Lambda deployment), you must request access to AI models in AWS Bedrock. **All labs will fail without this step.**

**Steps to Request Model Access:**
1. **Go to AWS Console** → **Amazon Bedrock**
2. **Navigate to Model Access** (in the left sidebar)
3. **Request access** for the following models:
   - **Claude 3.5 Sonnet v2** (anthropic.claude-3-5-sonnet-20241022-v2:0)
   - **Claude 3 Sonnet** (anthropic.claude-3-sonnet-20240229-v1:0) - *fallback option*
4. **Submit the request** - approval is typically instant for most accounts
5. **Wait for "Access granted"** status before proceeding with any lab

**Important Notes:**
- Model access is **region-specific** - request access in the same region where you plan to deploy
- **Recommended region**: `us-west-2` (used in all lab examples)
- This applies to **all deployment scenarios**: local testing, EC2, ECS containers, and Lambda functions
- Without model access, the application will return errors when attempting to generate summaries

**Verification**: You can verify model access by running:
```bash
aws bedrock list-foundation-models --region us-west-2
```

### Local Development and Testing

Before deploying to AWS, you can run and test the application locally. This requires the AWS CLI setup completed in the previous section.

#### Running the Application Locally

1. **Clone and setup**:
   ```bash
   git clone <repository-url>
   cd LectureSummarizer
   ```

2. **Test Backend API**:
   ```bash
   # Navigate to API project
   cd LectureSummarizer.API
   
   # Run the API
   dotnet run
   
   # Test health endpoint in another terminal
   curl http://localhost:5131/health
   
   # API will be available at http://localhost:5131
   ```

3. **Test Razor Frontend**:
   ```bash
   # Navigate to Web project (new terminal)
   cd LectureSummarizer.Web
   
   # Run the web application
   dotnet run
   
   # Web app will be available at http://localhost:5235
   ```

4. **Test Blazor SPA Frontend**:
   ```bash
   # Navigate to SPA project (new terminal)
   cd LectureSummarizer.Web.SPA
   
   # Run the SPA
   dotnet run
   
   # SPA will be available at http://localhost:5252
   ```

5. **End-to-End Testing**:
   - Visit either frontend URL
   - Upload a sample PDF lecture file
   - Verify AI summarization works with AWS Bedrock

⚠️ **Note**: The same AWS credentials created for local testing can be reused for Lab deployments, though you may need additional permissions for specific AWS services (ECS, Lambda, etc.).

### Quick Start

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd LectureSummarizer
   ```

2. **Choose your learning path**:
   - **Sequential Learning**: Start with Lab 1 → Lab 2 → Lab 3
   - **Specific Interest**: Jump to any lab based on your architecture preference
   - **Comparison Study**: Deploy all three to compare approaches

3. **Setup AWS credentials** (detailed in each lab)

4. **Follow the lab guides**:
   - [Lab 1: EC2 Split Deployment](lab1-ec2-deployment.md)
   - [Lab 2: ECS + S3 Deployment](lab2-ecs-s3-deployment.md)
   - [Lab 3: Lambda + S3 Deployment](lab3-lambda-s3-deployment.md)

## Learning Outcomes

By completing these labs, you will gain practical experience with:

**Cloud Architecture Patterns:**
- Traditional infrastructure management
- Container orchestration strategies
- Serverless computing paradigms

**AWS Services:**
- EC2, ECS Fargate, Lambda compute options
- Application Load Balancer and API Gateway
- S3 static hosting and CloudFront CDN
- IAM security and CloudWatch monitoring

**Modern Development Practices:**
- Infrastructure as Code with CloudFormation/SAM
- Container-based deployments
- Cross-origin resource sharing (CORS) configuration
- Production monitoring and logging

**Cost Optimization:**
- Understanding different pricing models
- Right-sizing infrastructure for workload patterns
- Balancing performance with cost efficiency

## Real-World Applications

These patterns reflect actual enterprise decisions:

- **Startups** often begin with **Lab 3 (Serverless)** for cost efficiency and rapid iteration
- **Enterprises** may use **Lab 1 (EC2)** for legacy applications and compliance requirements  
- **Modern SaaS companies** typically adopt **Lab 2 (Containers)** for microservices architectures

The skills learned in these labs directly apply to production systems handling millions of requests and users worldwide.

---

**Ready to begin?** Choose your starting lab and dive into hands-on AWS deployment experience! 🚀
