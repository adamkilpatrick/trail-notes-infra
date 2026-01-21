using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrailNotesInfra
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            var mainInfraStack = new TrailNotesInfraStack(app, "TrailNotesInfraStack", new StackProps
            {
            });

            var deadManSwitchStack = new DeadManSwitchInfraStack(app, "DeadMansSwitchStack", new DeadManSwitchInfraStackProps
            {
                WebsiteBucket = mainInfraStack.WebsiteBucket,
                WebsiteDistribution = mainInfraStack.WebsiteDistribution
            });
            app.Synth();
        }
    }
}
