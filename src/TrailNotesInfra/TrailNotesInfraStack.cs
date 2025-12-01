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
                WebsiteIndexDocument = "index.html",
                BlockPublicAccess = BlockPublicAccess.BLOCK_ACLS_ONLY,
                PublicReadAccess = true
            });
            var distribution = new Distribution(this, "website-distribution", new DistributionProps
            {
                Certificate = websiteCert,
                DomainNames = DOMAIN_ALIASES,
                DefaultBehavior = new BehaviorOptions
                {
                    Origin = new S3StaticWebsiteOrigin(websiteBucket)
                }
            });

            websiteBucket.GrantReadWrite(githubBucketRole);
            distribution.GrantCreateInvalidation(githubBucketRole);

            var bucketOutput = new CfnOutput(this, "website-bucket-name", new CfnOutputProps
            {
                Value = websiteBucket.BucketName
            });
            var distributionOutput = new CfnOutput(this, "website-distribution-id", new CfnOutputProps
            {
                Value = distribution.DistributionId
            });
        }
    }
}
