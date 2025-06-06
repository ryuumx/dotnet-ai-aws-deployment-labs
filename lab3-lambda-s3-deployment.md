# Lab 3: Lambda + S3 Deployment - Serverless Backend + Static Frontend

## Overview

**Objective**: Deploy the .NET Lecture Summarizer using a fully serverless architecture with AWS Lambda for the backend API and S3 + CloudFront for the Blazor WebAssembly frontend.

**Architecture**:
```
Internet → CloudFront → S3 (Blazor SPA)
        ↓
       API Gateway → Lambda Function (.NET) → AWS Bedrock
```

**Learning Objectives:**
- Serverless computing with AWS Lambda
- API Gateway configuration and integration
- **Infrastructure as Code (IaC)** with AWS SAM framework
- Lambda function optimization for .NET
- Event-driven architecture patterns
- Cost-effective serverless deployment

### Infrastructure as Code (IaC) Focus

This lab introduces **Infrastructure as Code (IaC)** concepts using **AWS SAM (Serverless Application Model)**. Unlike Labs 1 and 2 where we created resources manually through the AWS Console, this lab defines infrastructure using declarative templates.

**Key IaC Benefits**:
- 🔄 **Reproducible Deployments**: Same infrastructure every time
- 📝 **Version Control**: Track infrastructure changes in Git
- 🧪 **Testing**: Validate infrastructure before deployment
- 🚀 **Automation**: Deploy with single commands
- 🔄 **Rollback**: Easy rollback to previous versions

**Learn More About IaC**:
- [AWS Infrastructure as Code](https://aws.amazon.com/what-is/iac/)
- [AWS SAM Documentation](https://docs.aws.amazon.com/serverless-application-model/)
- [CloudFormation Best Practices](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/best-practices.html)

## Prerequisites

- Completed main lab setup (LectureSummarizer solution)
- **AWS SAM CLI installed** - [Install SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- **Docker installed and running locally** - [Install Docker Desktop](https://docs.docker.com/get-docker/)
- Basic understanding of serverless concepts
- **Note**: This lab can be completed independently without Labs 1 or 2
- Optionally: If you completed Labs 1 or 2, some resources (IAM roles) can be reused

### AWS Credentials Setup

⚠️ **Note**: If you completed **Lab 2 (ECS + S3)**, you may already have suitable AWS credentials configured. You can reuse the `lecture-lab-deployer` user from Lab 2, or create a new user with serverless-specific permissions as shown below.

⚠️ **Security Best Practice**: Create a dedicated IAM user for this lab instead of using Administrator access.

#### Step 1: Create IAM User (AWS Console) - Skip if using Lab 2 credentials

💡 **If reusing Lab 2 credentials**: The `lecture-lab-deployer` user from Lab 2 has most required permissions. You may need to add `AWSLambda_FullAccess` and `CloudFormationFullAccess` policies.

1. **Navigate to IAM Console** → **Users** → **Create user**

2. **User Details**:
   - **User name**: `lecture-serverless-deployer`
   - **Provide user access to AWS Management Console**: Unchecked (API access only)

3. **Set Permissions**:
   - **Attach policies directly**
   - **Add the following managed policies**:
     - `AWSLambda_FullAccess`
     - `AmazonAPIGatewayAdministrator`
     - `AmazonS3FullAccess`
     - `CloudFormationFullAccess`
     - `IAMFullAccess` (for SAM to create roles)
     - `CloudWatchFullAccess`
     - `CloudFrontFullAccess`
   - **Note**: These are broader permissions for lab convenience. In production, use more restrictive policies.

4. **Create User**

#### Step 2: Create Access Keys - Skip if using Lab 2 credentials

1. **Select your user** → **Security credentials** tab

2. **Create access key**:
   - **Use case**: Command Line Interface (CLI)
   - **Confirmation**: Check the box
   - **Description tag**: `Lab3-Serverless-Deployment` (optional)

3. **Save Credentials**:
   - **Access key ID**: Save this value
   - **Secret access key**: Save this value
   - **Download .csv file** for backup

#### Step 3: Configure AWS CLI and SAM

```bash
# Configure AWS CLI with your lab user credentials (skip if already done in Lab 2)
aws configure

# Enter your values:
# AWS Access Key ID: [Your Access Key ID]
# AWS Secret Access Key: [Your Secret Access Key]  
# Default region name: us-west-2
# Default output format: json

# Verify AWS configuration
aws sts get-caller-identity

# Verify SAM CLI installation
sam --version

# Expected: SAM CLI version information
# If not installed, follow the SAM CLI installation guide

# Verify Docker is running (should already be done if you completed Lab 2)
docker --version
docker info
```

## Step 1: Prepare Lambda Function

### 1.1 Create Lambda-Specific API Project

📁 **Create in**: `LectureSummarizer/` root directory

```bash
# Navigate to solution root
cd LectureSummarizer

# Create new Lambda project
dotnet new lambda.NativeAOT -n LectureSummarizer.Lambda
dotnet sln add LectureSummarizer.Lambda
cd LectureSummarizer.Lambda
dotnet add reference ../LectureSummarizer.Shared
```

### 1.2 Install Required NuGet Packages

```bash
# Navigate to Lambda project directory
cd LectureSummarizer.Lambda

# Add required packages
dotnet add package AWSSDK.BedrockRuntime
dotnet add package iTextSharp.LGPLv2.Core
dotnet add package Amazon.Lambda.APIGatewayEvents
dotnet add package Amazon.Lambda.Core
dotnet add package Amazon.Lambda.Serialization.SystemTextJson
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging
```

### 1.3 Create Lambda Function Code

**LectureSummarizer.Lambda/Function.cs**
```csharp
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Serialization.SystemTextJson;
using LectureSummarizer.Lambda.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using LectureSummarizer.Shared.Models;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace LectureSummarizer.Lambda;

public class Function
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Function> _logger;

    public Function()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<Function>>();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());
        services.AddAWSService<Amazon.BedrockRuntime.IAmazonBedrockRuntime>();
        services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IBedrockService, BedrockService>();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        _logger.LogInformation($"Processing request: {request.HttpMethod} {request.Path}");

        try
        {
            return request.Path.ToLower() switch
            {
                "/health" => await HandleHealthCheck(request, context),
                "/summarize" => await HandleSummarize(request, context),
                _ => CreateResponse(404, "Not Found")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request");
            return CreateResponse(500, "Internal Server Error");
        }
    }

    private async Task<APIGatewayProxyResponse> HandleHealthCheck(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var response = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "lecture-summarizer-lambda",
            requestId = context.AwsRequestId
        };

        return CreateResponse(200, JsonSerializer.Serialize(response));
    }

    private async Task<APIGatewayProxyResponse> HandleSummarize(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request.HttpMethod != "POST")
        {
            return CreateResponse(405, "Method Not Allowed");
        }

        try
        {
            // Parse multipart form data
            var fileData = ParseMultipartFormData(request);
            if (fileData == null)
            {
                return CreateResponse(400, JsonSerializer.Serialize(new SummaryResponse
                {
                    Success = false,
                    ErrorMessage = "No file uploaded or invalid file format."
                }));
            }

            var pdfExtractor = _serviceProvider.GetRequiredService<IPdfTextExtractor>();
            var bedrockService = _serviceProvider.GetRequiredService<IBedrockService>();

            // Extract text from PDF
            var extractedText = await pdfExtractor.ExtractTextAsync(fileData.Content);
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return CreateResponse(400, JsonSerializer.Serialize(new SummaryResponse
                {
                    Success = false,
                    ErrorMessage = "Unable to extract text from PDF."
                }));
            }

            // Generate summary using Bedrock
            var summary = await bedrockService.SummarizeLectureAsync(extractedText);

            var response = new SummaryResponse
            {
                Success = true,
                Summary = summary,
                FileName = fileData.FileName
            };

            return CreateResponse(200, JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing summarize request");
            return CreateResponse(500, JsonSerializer.Serialize(new SummaryResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while processing your request."
            }));
        }
    }

    private static FileData? ParseMultipartFormData(APIGatewayProxyRequest request)
    {
        if (request.Body == null || !request.Headers.ContainsKey("content-type"))
            return null;

        var contentType = request.Headers["content-type"];
        if (!contentType.StartsWith("multipart/form-data"))
            return null;

        try
        {
            var body = request.IsBase64Encoded 
                ? Convert.FromBase64String(request.Body)
                : System.Text.Encoding.UTF8.GetBytes(request.Body);

            // Simple multipart parser for PDF files
            // In production, consider using a more robust multipart parser
            var bodyString = System.Text.Encoding.UTF8.GetString(body);
            var boundary = ExtractBoundary(contentType);
            
            if (string.IsNullOrEmpty(boundary))
                return null;

            var parts = bodyString.Split(new[] { $"--{boundary}" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                if (part.Contains("Content-Type: application/pdf"))
                {
                    var lines = part.Split('\n');
                    var fileName = ExtractFileName(part);
                    
                    // Find the start of binary content
                    var contentStart = part.IndexOf("\r\n\r\n") + 4;
                    if (contentStart > 3)
                    {
                        var binaryContent = body.Skip(bodyString.IndexOf(part) + contentStart)
                                                .TakeWhile(b => b != 0x2D) // Stop at next boundary
                                                .ToArray();
                        
                        return new FileData
                        {
                            FileName = fileName ?? "unknown.pdf",
                            Content = binaryContent
                        };
                    }
                }
            }
        }
        catch (Exception)
        {
            // Log error but don't expose details
        }

        return null;
    }

    private static string? ExtractBoundary(string contentType)
    {
        var boundaryIndex = contentType.IndexOf("boundary=");
        if (boundaryIndex == -1) return null;
        
        return contentType.Substring(boundaryIndex + 9).Trim('"');
    }

    private static string? ExtractFileName(string part)
    {
        var match = System.Text.RegularExpressions.Regex.Match(part, @"filename=""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static APIGatewayProxyResponse CreateResponse(int statusCode, string body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = body,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = statusCode == 200 && body.StartsWith("{") ? "application/json" : "text/plain",
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Headers"] = "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token",
                ["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS"
            }
        };
    }

    private class FileData
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
```

### 1.4 Copy Service Classes from Original API

**LectureSummarizer.Lambda/Services/IPdfTextExtractor.cs**
```csharp
namespace LectureSummarizer.Lambda.Services
{
    public interface IPdfTextExtractor
    {
        Task<string> ExtractTextAsync(byte[] pdfContent);
    }
}
```

**LectureSummarizer.Lambda/Services/PdfTextExtractor.cs**
```csharp
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;

namespace LectureSummarizer.Lambda.Services
{
    public class PdfTextExtractor : IPdfTextExtractor
    {
        public async Task<string> ExtractTextAsync(byte[] pdfContent)
        {
            return await Task.Run(() =>
            {
                var text = new StringBuilder();
                
                using (var reader = new PdfReader(pdfContent))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                    }
                }
                
                return text.ToString();
            });
        }
    }
}
```

**LectureSummarizer.Lambda/Services/IBedrockService.cs**
```csharp
namespace LectureSummarizer.Lambda.Services
{
    public interface IBedrockService
    {
        Task<string> SummarizeLectureAsync(string lectureText);
    }
}
```

**LectureSummarizer.Lambda/Services/BedrockService.cs**
```csharp
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text;
using System.Text.Json;

namespace LectureSummarizer.Lambda.Services
{
    public class BedrockService : IBedrockService
    {
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private const string ModelId = "anthropic.claude-3-5-sonnet-20241022-v2:0";

        public BedrockService(IAmazonBedrockRuntime bedrockClient)
        {
            _bedrockClient = bedrockClient;
        }

        public async Task<string> SummarizeLectureAsync(string lectureText)
        {
            var prompt = $@"Please provide a comprehensive summary of this lecture. Focus on:
1. Main topics and key concepts
2. Important points and takeaways
3. Any conclusions or recommendations

Lecture content:
{lectureText}

Please provide a well-structured summary in bullet points or short paragraphs:";

            var request = new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 1000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(request);
            var requestBody = Encoding.UTF8.GetBytes(jsonRequest);

            var invokeRequest = new InvokeModelRequest
            {
                ModelId = ModelId,
                Body = new MemoryStream(requestBody),
                ContentType = "application/json"
            };

            var response = await _bedrockClient.InvokeModelAsync(invokeRequest);
            
            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();
            
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var content = jsonResponse.GetProperty("content")[0].GetProperty("text").GetString();
            
            return content ?? "Unable to generate summary.";
        }
    }
}
```

## Step 2: Create SAM Template

### 2.1 Create SAM Template File

📁 **Create in**: `LectureSummarizer/` root directory

**LectureSummarizer/template.yaml**
```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: 'Lecture Summarizer - Serverless .NET application'

Globals:
  Function:
    Timeout: 30
    MemorySize: 512
    Runtime: provided.al2
    Architectures:
      - x86_64
    Environment:
      Variables:
        AWS_LAMBDA_HANDLER_LOG_LEVEL: Information

Parameters:
  Environment:
    Type: String
    Default: dev
    AllowedValues:
      - dev
      - prod
    Description: Environment name

Resources:
  # Lambda Function
  LectureSummarizerFunction:
    Type: AWS::Serverless::Function
    Properties:
      FunctionName: !Sub 'lecture-summarizer-${Environment}'
      CodeUri: LectureSummarizer.Lambda/
      Handler: bootstrap
      Description: 'Lecture Summarizer API Lambda Function'
      
      # IAM Role for Bedrock access
      Policies:
        - Version: '2012-10-17'
          Statement:
            - Effect: Allow
              Action:
                - bedrock:InvokeModel
                - bedrock:InvokeModelWithResponseStream
              Resource: 
                - !Sub 'arn:aws:bedrock:${AWS::Region}::foundation-model/anthropic.claude-3-5-sonnet-20241022-v2:0'
            - Effect: Allow
              Action:
                - logs:CreateLogGroup
                - logs:CreateLogStream
                - logs:PutLogEvents
              Resource: '*'
      
      # API Gateway Events
      Events:
        HealthCheck:
          Type: Api
          Properties:
            RestApiId: !Ref LectureSummarizerApi
            Path: /health
            Method: get
        
        SummarizePost:
          Type: Api
          Properties:
            RestApiId: !Ref LectureSummarizerApi
            Path: /summarize
            Method: post
        
        SummarizeOptions:
          Type: Api
          Properties:
            RestApiId: !Ref LectureSummarizerApi
            Path: /summarize
            Method: options

  # API Gateway
  LectureSummarizerApi:
    Type: AWS::Serverless::Api
    Properties:
      Name: !Sub 'lecture-summarizer-api-${Environment}'
      StageName: !Ref Environment
      Cors:
        AllowMethods: "'GET,POST,OPTIONS'"
        AllowHeaders: "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token'"
        AllowOrigin: "'*'"
        AllowCredentials: false
      BinaryMediaTypes:
        - 'multipart/form-data'
        - 'application/pdf'
      
      # API Gateway Configuration
      DefinitionBody:
        openapi: 3.0.1
        info:
          title: Lecture Summarizer API
          version: 1.0.0
        paths:
          /health:
            get:
              responses:
                '200':
                  description: Health check response
              x-amazon-apigateway-integration:
                httpMethod: POST
                type: aws_proxy
                uri: !Sub 'arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${LectureSummarizerFunction.Arn}/invocations'
          
          /summarize:
            post:
              requestBody:
                content:
                  multipart/form-data:
                    schema:
                      type: object
                      properties:
                        file:
                          type: string
                          format: binary
              responses:
                '200':
                  description: Summary response
              x-amazon-apigateway-integration:
                httpMethod: POST
                type: aws_proxy
                uri: !Sub 'arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${LectureSummarizerFunction.Arn}/invocations'
            
            options:
              responses:
                '200':
                  description: CORS preflight response
                  headers:
                    Access-Control-Allow-Origin:
                      schema:
                        type: string
                    Access-Control-Allow-Methods:
                      schema:
                        type: string
                    Access-Control-Allow-Headers:
                      schema:
                        type: string
              x-amazon-apigateway-integration:
                httpMethod: POST
                type: aws_proxy
                uri: !Sub 'arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${LectureSummarizerFunction.Arn}/invocations'

  # CloudWatch Log Group
  LectureSummarizerLogGroup:
    Type: AWS::Logs::LogGroup
    Properties:
      LogGroupName: !Sub '/aws/lambda/lecture-summarizer-${Environment}'
      RetentionInDays: 7

Outputs:
  LectureSummarizerApi:
    Description: 'API Gateway endpoint URL'
    Value: !Sub 'https://${LectureSummarizerApi}.execute-api.${AWS::Region}.amazonaws.com/${Environment}/'
    Export:
      Name: !Sub '${AWS::StackName}-ApiUrl'

  LectureSummarizerFunction:
    Description: 'Lambda Function ARN'
    Value: !GetAtt LectureSummarizerFunction.Arn
    Export:
      Name: !Sub '${AWS::StackName}-FunctionArn'
```

### 2.2 Create SAM Configuration

**LectureSummarizer/samconfig.toml**
```toml
version = 0.1

[default]
[default.deploy]
[default.deploy.parameters]
stack_name = "lecture-summarizer-serverless"
s3_bucket = ""  # SAM will create this automatically
s3_prefix = "lecture-summarizer"
region = "us-west-2"
confirm_changeset = true
capabilities = "CAPABILITY_IAM"
image_repositories = []
parameter_overrides = "Environment=dev"

[prod]
[prod.deploy]
[prod.deploy.parameters]
stack_name = "lecture-summarizer-serverless-prod"
s3_bucket = ""
s3_prefix = "lecture-summarizer-prod"
region = "us-west-2"
confirm_changeset = true
capabilities = "CAPABILITY_IAM"
parameter_overrides = "Environment=prod"
```

## Step 3: Build and Deploy Scripts

### 3.1 Create Build Scripts

📁 **Create in**: `LectureSummarizer/scripts/` directory

**scripts/build-lambda.sh**
```bash
#!/bin/bash

echo "🚀 Building Lambda function..."

# Navigate to Lambda project
cd LectureSummarizer.Lambda

# Restore and build
echo "📦 Restoring NuGet packages..."
dotnet restore

echo "🔨 Building Lambda function..."
dotnet build -c Release

if [ $? -eq 0 ]; then
    echo "✅ Lambda function built successfully"
    cd ..
else
    echo "❌ Lambda build failed"
    exit 1
fi
```

**scripts/deploy-serverless.sh**
```bash
#!/bin/bash

echo "🚀 Deploying serverless application with SAM..."

# Set environment (default to dev)
ENVIRONMENT=${1:-dev}

echo "📋 Deployment Configuration:"
echo "   Environment: ${ENVIRONMENT}"
echo "   Region: us-west-2"
echo "   Stack: lecture-summarizer-serverless${ENVIRONMENT:+-$ENVIRONMENT}"

# Build Lambda function first
echo "🔨 Building Lambda function..."
./scripts/build-lambda.sh

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

# SAM build
echo "📦 SAM Build..."
sam build

if [ $? -ne 0 ]; then
    echo "❌ SAM build failed"
    exit 1
fi

# SAM deploy
echo "🚀 SAM Deploy..."
if [ "$ENVIRONMENT" == "prod" ]; then
    sam deploy --config-env prod
else
    sam deploy --config-env default
fi

if [ $? -eq 0 ]; then
    echo "✅ Deployment successful!"
    
    # Get API Gateway URL
    API_URL=$(aws cloudformation describe-stacks \
        --stack-name lecture-summarizer-serverless${ENVIRONMENT:+-$ENVIRONMENT} \
        --query 'Stacks[0].Outputs[?OutputKey==`LectureSummarizerApi`].OutputValue' \
        --output text)
    
    echo "📋 Deployment Information:"
    echo "   API Gateway URL: ${API_URL}"
    echo "   Health Check: ${API_URL}health"
    echo "   Summarize Endpoint: ${API_URL}summarize"
    
    echo "📋 Next Steps:"
    echo "   1. Test API: curl ${API_URL}health"
    echo "   2. Update SPA configuration with API URL"
    echo "   3. Deploy SPA to S3"
else
    echo "❌ Deployment failed"
    exit 1
fi
```

**scripts/test-lambda.sh**
```bash
#!/bin/bash

echo "🧪 Testing Lambda function locally..."

# Check if sam is available
if ! command -v sam &> /dev/null; then
    echo "❌ SAM CLI not found. Please install SAM CLI first."
    exit 1
fi

# Build first
echo "🔨 Building for local testing..."
sam build

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

# Start local API
echo "🌐 Starting local API Gateway..."
echo "📋 Local endpoints will be available at:"
echo "   Health Check: http://127.0.0.1:3000/health"
echo "   Summarize: http://127.0.0.1:3000/summarize"
echo ""
echo "💡 Press Ctrl+C to stop the local server"
echo "💡 Test with: curl http://127.0.0.1:3000/health"

sam local start-api --port 3000
```

### 3.2 Update SPA for Serverless Backend

**LectureSummarizer.Web.SPA/wwwroot/appsettings.json**
```json
{
  "ApiBaseUrl": "https://your-api-id.execute-api.us-west-2.amazonaws.com/dev"
}
```

**scripts/build-spa-serverless.sh**
```bash
#!/bin/bash

echo "🌐 Building Blazor SPA for serverless deployment..."

# Get API Gateway URL from CloudFormation stack
API_URL=$(aws cloudformation describe-stacks \
    --stack-name lecture-summarizer-serverless \
    --query 'Stacks[0].Outputs[?OutputKey==`LectureSummarizerApi`].OutputValue' \
    --output text 2>/dev/null)

if [ -z "$API_URL" ]; then
    echo "⚠️ Could not get API URL from CloudFormation. Using placeholder."
    echo "💡 Make sure to update appsettings.json manually after deployment."
    API_URL="https://your-api-id.execute-api.us-west-2.amazonaws.com/dev/"
fi

echo "📋 Configuration:"
echo "   API URL: ${API_URL}"

# Update SPA configuration
cat > LectureSummarizer.Web.SPA/wwwroot/appsettings.json <<EOF
{
  "ApiBaseUrl": "${API_URL}"
}
EOF

# Clean previous builds
rm -rf ./publish/spa
rm -f spa-deployment.zip

# Build and publish the SPA
echo "🔨 Building Blazor WebAssembly..."
dotnet publish LectureSummarizer.Web.SPA -c Release -o ./publish/spa

if [ $? -eq 0 ]; then
    echo "✅ SPA built successfully"
    
    # Create deployment package
    cd ./publish/spa/wwwroot
    zip -r ../../../spa-deployment.zip .
    cd ../../../
    
    echo "📦 Deployment package created: spa-deployment.zip"
    echo "📋 Size: $(du -h spa-deployment.zip | cut -f1)"
    
    echo "📋 Next steps:"
    echo "   1. Create S3 bucket: aws s3 mb s3://lecture-summarizer-spa-serverless-[random-suffix]"
    echo "   2. Upload files: aws s3 sync ./publish/spa/wwwroot s3://your-bucket-name"
    echo "   3. Configure bucket for static hosting"
else
    echo "❌ SPA build failed"
    exit 1
fi
```

## Step 4: Deploy Serverless Backend

### 4.1 Build and Deploy Lambda Function

```bash
# Navigate to solution root
cd LectureSummarizer

# Make scripts executable
chmod +x scripts/*.sh

# Test local build first
./scripts/build-lambda.sh

# Test locally (optional)
./scripts/test-lambda.sh
# In another terminal: curl http://127.0.0.1:3000/health
# Press Ctrl+C to stop local server

# Deploy to AWS
./scripts/deploy-serverless.sh dev
```

### 4.2 Verify Lambda Deployment

```bash
# Test health endpoint
API_URL=$(aws cloudformation describe-stacks \
    --stack-name lecture-summarizer-serverless \
    --query 'Stacks[0].Outputs[?OutputKey==`LectureSummarizerApi`].OutputValue' \
    --output text)

echo "Testing API at: ${API_URL}health"
curl "${API_URL}health"

# Expected response:
# {"status":"healthy","timestamp":"2024-01-15T10:30:00Z","service":"lecture-summarizer-lambda","requestId":"..."}
```

### 4.3 Monitor Lambda Function

```bash
# View Lambda logs
aws logs tail /aws/lambda/lecture-summarizer-dev --follow

# Check Lambda function metrics
aws lambda get-function --function-name lecture-summarizer-dev

# Monitor API Gateway metrics in AWS Console
# Go to API Gateway → lecture-summarizer-api-dev → Monitoring
```

## Step 5: Deploy Frontend to S3

### 5.1 Create S3 Bucket for Static Website

1. **Navigate to S3 Console** → **Create bucket**

2. **Configure Bucket**:
   - **Bucket name**: `lecture-summarizer-spa-serverless-[random-suffix]` (must be globally unique)
   - **Region**: us-west-2
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
# Build SPA with correct API configuration
./scripts/build-spa-serverless.sh

# Upload to S3 (replace with your bucket name)
aws s3 sync ./publish/spa/wwwroot s3://lecture-summarizer-spa-serverless-[random-suffix] --delete

# Verify upload
aws s3 ls s3://lecture-summarizer-spa-serverless-[random-suffix] --recursive

# Get website URL
echo "Website URL: http://lecture-summarizer-spa-serverless-[random-suffix].s3-website-us-west-2.amazonaws.com"
```

### 5.5 Test S3 Website

1. **Get website URL** from S3 Console → Properties → Static website hosting
2. **Visit the URL** in your browser
3. **Test file upload** with a sample PDF
4. **Verify AI summarization** works end-to-end

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

6. **Wait for deployment** (Status: Deployed - takes 5-15 minutes)

### 6.2 Update CORS for CloudFront

The Lambda function already includes wildcard CORS headers, but you can make it more specific:

**Update LectureSummarizer.Lambda/Function.cs** in the `CreateResponse` method:
```csharp
private static APIGatewayProxyResponse CreateResponse(int statusCode, string body)
{
    return new APIGatewayProxyResponse
    {
        StatusCode = statusCode,
        Body = body,
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = statusCode == 200 && body.StartsWith("{") ? "application/json" : "text/plain",
            ["Access-Control-Allow-Origin"] = "*", // Or specific CloudFront domain
            ["Access-Control-Allow-Headers"] = "Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token",
            ["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS"
        }
    };
}
```

## Step 7: Advanced Configuration and Optimization

### 7.1 Lambda Function Optimization

**Update template.yaml for better performance**:
```yaml
LectureSummarizerFunction:
  Type: AWS::Serverless::Function
  Properties:
    FunctionName: !Sub 'lecture-summarizer-${Environment}'
    CodeUri: LectureSummarizer.Lambda/
    Handler: bootstrap
    Description: 'Lecture Summarizer API Lambda Function'
    Timeout: 30
    MemorySize: 1024  # Increased for better PDF processing
    
    # Reserved concurrency to control costs
    ReservedConcurrencyLimit: 5
    
    # Environment variables
    Environment:
      Variables:
        AWS_LAMBDA_HANDLER_LOG_LEVEL: Information
        LAMBDA_NET_SERIALIZER_DEBUG: false
```

### 7.2 API Gateway Configuration

**Add request validation and throttling**:
```yaml
LectureSummarizerApi:
  Type: AWS::Serverless::Api
  Properties:
    Name: !Sub 'lecture-summarizer-api-${Environment}'
    StageName: !Ref Environment
    
    # Throttling configuration
    ThrottleConfig:
      RateLimit: 100
      BurstLimit: 200
    
    # Request validation
    RequestValidatorId: !Ref RequestValidator
    
    # CORS configuration
    Cors:
      AllowMethods: "'GET,POST,OPTIONS'"
      AllowHeaders: "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token'"
      AllowOrigin: "'*'"
      AllowCredentials: false
    
    BinaryMediaTypes:
      - 'multipart/form-data'
      - 'application/pdf'

# Request Validator
RequestValidator:
  Type: AWS::ApiGateway::RequestValidator
  Properties:
    RestApiId: !Ref LectureSummarizerApi
    ValidateRequestBody: true
    ValidateRequestParameters: true
```

### 7.3 Monitoring and Alerting

**Add CloudWatch alarms**:
```yaml
# Lambda Error Alarm
LambdaErrorAlarm:
  Type: AWS::CloudWatch::Alarm
  Properties:
    AlarmName: !Sub 'lecture-summarizer-${Environment}-errors'
    AlarmDescription: 'Lambda function errors'
    MetricName: Errors
    Namespace: AWS/Lambda
    Statistic: Sum
    Period: 300
    EvaluationPeriods: 1
    Threshold: 5
    ComparisonOperator: GreaterThanThreshold
    Dimensions:
      - Name: FunctionName
        Value: !Ref LectureSummarizerFunction

# API Gateway 4XX Errors
ApiGateway4XXAlarm:
  Type: AWS::CloudWatch::Alarm
  Properties:
    AlarmName: !Sub 'lecture-summarizer-api-${Environment}-4xx'
    AlarmDescription: 'API Gateway 4XX errors'
    MetricName: 4XXError
    Namespace: AWS/ApiGateway
    Statistic: Sum
    Period: 300
    EvaluationPeriods: 2
    Threshold: 10
    ComparisonOperator: GreaterThanThreshold
    Dimensions:
      - Name: ApiName
        Value: !Ref LectureSummarizerApi
```

## Step 8: Testing and Verification

### 8.1 Comprehensive Testing

**Test Health Endpoint**:
```bash
# Get API URL
API_URL=$(aws cloudformation describe-stacks \
    --stack-name lecture-summarizer-serverless \
    --query 'Stacks[0].Outputs[?OutputKey==`LectureSummarizerApi`].OutputValue' \
    --output text)

# Test health check
curl "${API_URL}health"
```

**Test CORS Preflight**:
```bash
# Test OPTIONS request
curl -X OPTIONS "${API_URL}summarize" \
  -H "Origin: https://your-cloudfront-domain.cloudfront.net" \
  -H "Access-Control-Request-Method: POST" \
  -H "Access-Control-Request-Headers: Content-Type" \
  -v
```

**Load Testing** (optional):
```bash
# Install artillery for load testing
npm install -g artillery

# Create artillery config
cat > load-test.yml <<EOF
config:
  target: '${API_URL}'
  phases:
    - duration: 60
      arrivalRate: 5
scenarios:
  - name: "Health check load test"
    requests:
      - get:
          url: "/health"
EOF

# Run load test
artillery run load-test.yml
```

### 8.2 Monitoring Commands

```bash
# View Lambda function logs
aws logs tail /aws/lambda/lecture-summarizer-dev --follow

# Get Lambda function metrics
aws cloudwatch get-metric-statistics \
    --namespace AWS/Lambda \
    --metric-name Invocations \
    --dimensions Name=FunctionName,Value=lecture-summarizer-dev \
    --statistics Sum \
    --start-time $(date -d '1 hour ago' -u +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
    --period 300

# Monitor API Gateway requests
aws cloudwatch get-metric-statistics \
    --namespace AWS/ApiGateway \
    --metric-name Count \
    --dimensions Name=ApiName,Value=lecture-summarizer-api-dev \
    --statistics Sum \
    --start-time $(date -d '1 hour ago' -u +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
    --period 300
```

### 8.3 Troubleshooting Common Issues

**Lambda Cold Start Issues**:
```bash
# Check function duration
aws logs filter-log-events \
    --log-group-name /aws/lambda/lecture-summarizer-dev \
    --filter-pattern "REPORT RequestId" \
    --start-time $(date -d '1 hour ago' +%s)000
```

**API Gateway Issues**:
```bash
# Check API Gateway logs (if enabled)
aws logs describe-log-groups --log-group-name-prefix "API-Gateway-Execution-Logs"

# Test API Gateway directly
aws apigateway test-invoke-method \
    --rest-api-id your-api-id \
    --resource-id your-resource-id \
    --http-method GET \
    --path-with-query-string "/health"
```

## Step 9: Cleanup Resources (Optional)

💡 **Note**: This cleanup is **optional**. You may want to keep resources for further testing.

### 9.1 Delete CloudFormation Stack

```bash
# Delete the entire serverless stack
aws cloudformation delete-stack --stack-name lecture-summarizer-serverless

# Monitor deletion progress
aws cloudformation describe-stacks --stack-name lecture-summarizer-serverless \
    --query 'Stacks[0].StackStatus'
```

### 9.2 Delete S3 Bucket and CloudFront

```bash
# Empty S3 bucket first
aws s3 rm s3://lecture-summarizer-spa-serverless-[random-suffix] --recursive

# Delete S3 bucket
aws s3 rb s3://lecture-summarizer-spa-serverless-[random-suffix]

# Delete CloudFront distribution (must be disabled first)
# This is done through AWS Console as it requires multiple steps
```

### 9.3 Delete SAM Build Artifacts

```bash
# Clean up local build artifacts
rm -rf .aws-sam/
rm -rf LectureSummarizer.Lambda/bin/
rm -rf LectureSummarizer.Lambda/obj/
rm -rf publish/
rm -f spa-deployment.zip
```

### 9.4 Cleanup IAM User (Optional)

If you no longer need the serverless deployer user:
1. **IAM Console** → **Users** → `lecture-serverless-deployer`
2. **Security credentials** → **Delete access keys**
3. **Delete user**

## Summary

This lab demonstrated:

✅ **Serverless Architecture**: Lambda functions with API Gateway  
✅ **Infrastructure as Code**: SAM templates and CloudFormation  
✅ **Modern .NET Deployment**: Native AOT Lambda functions  
✅ **API Integration**: RESTful API with proper CORS configuration  
✅ **Static Site Hosting**: S3 + CloudFront for global performance  
✅ **Monitoring & Logging**: CloudWatch integration  
✅ **Cost Optimization**: Pay-per-request serverless model  

**Key Benefits of Serverless Architecture**:
- 💰 **Cost-Effective**: Pay only for actual requests and compute time
- 🔄 **Auto-Scaling**: Automatically handles traffic spikes
- 🛠️ **No Server Management**: Focus on code, not infrastructure
- 🌍 **Global Distribution**: CloudFront edge locations worldwide
- 🔒 **Built-in Security**: IAM-based access control
- ⚡ **Fast Deployment**: Quick iterations with SAM CLI

**Production Considerations**:
- **Cold Start Optimization**: Use provisioned concurrency for critical functions
- **Error Handling**: Implement retry logic and dead letter queues
- **Security**: Use API keys or JWT tokens for authentication
- **Monitoring**: Set up comprehensive CloudWatch alarms
- **Cost Management**: Monitor Lambda invocations and duration
