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
    public class DeadManSwitchInfraStack : Stack
    {
        internal DeadManSwitchInfraStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var bucket = new Bucket(this, "dead-man-switch-bucket", new BucketProps
            {
            });
        }
    }
}