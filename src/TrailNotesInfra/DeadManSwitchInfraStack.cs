using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace TrailNotesInfra
{
    public class DeadManSwitchInfraStack : Stack
    {
        public Amazon.CDK.AWS.Lambda.Function DeadManLambda { get; private set; }

        internal DeadManSwitchInfraStack(Construct scope, string id, DeadManSwitchInfraStackProps props = null) : base(scope, id, props)
        {
            var bucket = new Bucket(this, "dead-man-switch-bucket", new BucketProps
            {
            });

            this.DeadManLambda = new Amazon.CDK.AWS.Lambda.Function(this, "dead-man-lambda", new Amazon.CDK.AWS.Lambda.FunctionProps
            {
                Runtime = Runtime.PYTHON_3_12,
                Handler = "deadManSwitch.lambda_handler",
                Code = Code.FromAsset("src/TrailNotesInfra/scripts"),
                Environment = new Dictionary<string, string>
                {
                    { "WEB_SITE_BUCKET", props.WebsiteBucket.BucketName },
                    { "DEAD_MAN_BUCKET", bucket.BucketName },
                    { "DEAD_MAN_KEY", "/payload.zip" },
                    { "DAYS_THRESHOLD", "15" },
                    { "CLOUDFRONT_DISTRIBUTION_ID", props.WebsiteDistribution.DistributionId }
                },
                Timeout = Duration.Minutes(5)
            });

            bucket.GrantReadWrite(this.DeadManLambda);
            props.WebsiteBucket.GrantReadWrite(this.DeadManLambda);
            props.WebsiteDistribution.GrantCreateInvalidation(this.DeadManLambda);
            var dailyRule = new Rule(this, "daily-dead-check", new RuleProps
            {
                Schedule = Schedule.Cron(new CronOptions
                {
                    Hour = "4",
                    Minute = "0"
                })
            });
            dailyRule.AddTarget(new LambdaFunction(this.DeadManLambda));
        }
    }
}