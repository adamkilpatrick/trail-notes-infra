using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace TrailNotesInfra
{
    public class TrailNotesInfraStack : Stack
    {
        private static readonly string DOMAIN = "trail.snakeha.us";
        private static readonly string REPO = "adamkilpatrick/trail-notes";
        private static readonly string[] DOMAIN_ALIASES = { DOMAIN };

        internal TrailNotesInfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var githubBucketRole = new Role(this, "github-bucket-role", new RoleProps
            {
                AssumedBy = new FederatedPrincipal($"arn:aws:iam::{this.Account}:oidc-provider/token.actions.githubusercontent.com", new Dictionary<string, object>
                {
                    { 
                        "StringEquals", 
                        new Dictionary<string, object>
                        {
                            { "token.actions.githubusercontent.com:aud", "sts.amazonaws.com" }
                        }
                    },
                    {
                        "StringLike",
                        new Dictionary<string, object>
                        {
                            { "token.actions.githubusercontent.com:sub", $"repo:{REPO}:*" }
                        }
                    }
                }, 
                "sts:AssumeRoleWithWebIdentity")
            });
            var websiteCert = new Certificate(this, "website-cert", new CertificateProps
            {
                DomainName = DOMAIN,
                Validation = CertificateValidation.FromDns()
            });
            var websiteBucket = new Bucket(this, "website-bucket", new BucketProps
            {
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL
            });
            
            var resolveHtmlFunction = new Function(this, "resolve-html-function", new FunctionProps
            {
                Code = FunctionCode.FromInline("""
                    function handler(event) {
                        var request = event.request;
                        var uri = request.uri;

                        if (uri.endsWith('/')) {
                            request.uri += 'index.html';
                        } else if(!uri.includes('.')) {
                            request.uri += '.html';
                        }
                        return request;
                    }
                
                """)
            });
            var distribution = new Distribution(this, "website-distribution", new DistributionProps
            {
                Certificate = websiteCert,
                DomainNames = DOMAIN_ALIASES,
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = S3BucketOrigin.WithOriginAccessControl(websiteBucket),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    FunctionAssociations = new FunctionAssociation[]
                    {
                        new FunctionAssociation
                        {
                            Function = resolveHtmlFunction,
                            EventType = FunctionEventType.VIEWER_REQUEST
                        }
                    }
                },
            });


            var bucketUser = new User(this, "bucket-user");
            var accessKey = new CfnAccessKey(this, "bucket-user-key", new CfnAccessKeyProps
            {
                UserName = bucketUser.UserName
            });

            websiteBucket.GrantReadWrite(githubBucketRole);
            websiteBucket.GrantReadWrite(bucketUser);
            distribution.GrantCreateInvalidation(githubBucketRole);

            var bucketOutput = new CfnOutput(this, "website-bucket-name", new CfnOutputProps
            {
                Value = websiteBucket.BucketName
            });
            var distributionOutput = new CfnOutput(this, "website-distribution-id", new CfnOutputProps
            {
                Value = distribution.DistributionId
            });
            var accessKeyIdOutput = new CfnOutput(this, "bucket-user-access-key-id", new CfnOutputProps
            {
                Value = accessKey.Ref
            });
            var secretAccessKeyOutput = new CfnOutput(this, "bucket-user-secret-access-key", new CfnOutputProps
            {
                Value = accessKey.AttrSecretAccessKey
            });
        }
    }
}
