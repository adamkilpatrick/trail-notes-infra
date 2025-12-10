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
            new TrailNotesInfraStack(app, "TrailNotesInfraStack", new StackProps
            {
            });
            app.Synth();
        }
    }
}
