# Welcome to your CDK C# project!

This is a blank project for CDK development with C#.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

It uses the [.NET CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

## Useful commands

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template


## Additional Notes
- This obviously doesn't show whatever AWS acct is mine (God knows I don't remember/know what the acct number is anyways) but if you want to repro this stuff just setup an AWS profile locally via ENV vars or whatever and it should work
- The repo it is bound to is my own one just specified as a const but I could pull that into an ENV var and I might
- This uses ACM DNS based TLS cert verification. I use namecheap because... iunno, but you can use whatever, you just have to set up some CNAMEs when you do the first synth, it will yell at you
- This also requires an OIDC connection from Github to the AWS acct. In theory this could be done as IaaC but it is kind of a hassle, so I just set it up manually and jammed the ARN from the connection in here: `$"arn:aws:iam::{this.Account}:oidc-provider/token.actions.githubusercontent.com"`