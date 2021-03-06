﻿using Octopus.Cli.Model;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands
{
    public class ChannelVersionRuleTester : IChannelVersionRuleTester
    {
        public ChannelVersionRuleTestResult Test(IOctopusRepository repository, ChannelVersionRuleResource rule, string packageVersion)
        {
            if (rule == null)
            {
                // Anything goes if there is no rule defined for this step
                return ChannelVersionRuleTestResult.Null();
            }

            var link = repository.Client.RootDocument.Link("VersionRuleTest");

            var resource = new
            {
                version = packageVersion,
                versionRange = rule.VersionRange,
                preReleaseTag = rule.Tag
            };

            var response = repository.Client.RootDocument.UsePostForChannelVersionRuleTest()
                ? repository.Client.Post<object, ChannelVersionRuleTestResult>(link, resource)
                : repository.Client.Get<ChannelVersionRuleTestResult>(link, resource);

            return response;
        }
    }
}
