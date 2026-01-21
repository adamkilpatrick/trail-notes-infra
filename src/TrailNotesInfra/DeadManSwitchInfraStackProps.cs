using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;

namespace TrailNotesInfra
{
    public class DeadManSwitchInfraStackProps : StackProps
    {
        public Bucket WebsiteBucket { get; set; }
        public Distribution WebsiteDistribution { get; set; }
    }
}