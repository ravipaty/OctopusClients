﻿using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;

namespace Octopus.Cli.Commands
{
    [Command("list-machines", Description = "Lists all machines")]
    public class ListMachinesCommand : ApiCommand
    {
        readonly HashSet<string> environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> healthStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool? isDisabled;
        private bool? isCalamariOutdated;
        private bool? isTentacleOutdated;

        public ListMachinesCommand(IOctopusRepositoryFactory repositoryFactory, ILogger log, IOctopusFileSystem fileSystem)
            : base(repositoryFactory, log, fileSystem)
        {
            var options = Options.For("Listing");
            options.Add("environment=", "Name of an environment to filter by. Can be specified many times.", v => environments.Add(v));
            options.Add("status=", $"[Optional] Status of Machines filter by ({string.Join(", ", HealthStatusProvider.StatusNames)}). Can be specified many times.", v => statuses.Add(v));
            options.Add("health-status=|healthstatus=", $"[Optional] Health status of Machines filter by ({string.Join(", ", HealthStatusProvider.HealthStatusNames)}). Can be specified many times.", v => healthStatuses.Add(v));
            options.Add("disabled=", "[Optional] Disabled status filter of Machine.", v => SetFlagState(v, ref isDisabled));
            options.Add("calamari-outdated=", "[Optional] State of Calamari to filter. By default ignores Calamari state.", v => SetFlagState(v, ref isCalamariOutdated));
            options.Add("tentacle-outdated=", "[Optional] State of Tentacle version to filter. By default ignores Tentacle state", v => SetFlagState(v, ref isTentacleOutdated));
        }
        
        protected override void Execute()
        {
            var provider = new HealthStatusProvider(Repository, Log, statuses, healthStatuses);
            var environmentResources = GetEnvironments();
            var environmentMachines = FilterByEnvironments(environmentResources);
            environmentMachines = FilterByState(environmentMachines, provider);

            LogFilteredMachines(environmentMachines, provider, environmentResources);
        }

        private void LogFilteredMachines(IEnumerable<MachineResource> environmentMachines, HealthStatusProvider provider, List<EnvironmentResource> environmentResources)
        {
            var orderedMachines = environmentMachines.OrderBy(m => m.Name).ToList();
            Log.Information("Machines: " + orderedMachines.Count);
            foreach (var machine in orderedMachines)
            {
                Log.Information(" - {0} {1} (ID: {2}) in {3}", machine.Name, provider.GetStatus(machine), machine.Id,
                    string.Join(" and ", machine.EnvironmentIds.Select(id => environmentResources.First(e => e.Id == id).Name)));
            }
        }

        private List<EnvironmentResource> GetEnvironments()
        {
            Log.Debug("Loading environments...");
            var environmentResources = Repository.Environments.FindAll();
            return environmentResources;
        }

        private IEnumerable<MachineResource> FilterByState(IEnumerable<MachineResource> environmentMachines, HealthStatusProvider provider)
        {
            environmentMachines = provider.Filter(environmentMachines);

            if (isDisabled.HasValue)
            {
                environmentMachines = environmentMachines.Where(m => m.IsDisabled == isDisabled.Value);
            }
            if (isCalamariOutdated.HasValue)
            {
                environmentMachines = environmentMachines.Where(m => m.HasLatestCalamari == !isCalamariOutdated.Value);
            }
            if (isTentacleOutdated.HasValue)
            {
                environmentMachines =
                    environmentMachines.Where(
                        m =>
                            (m.Endpoint as ListeningTentacleEndpointResource)?.TentacleVersionDetails.UpgradeSuggested ==
                            isTentacleOutdated.Value);
            }
            return environmentMachines;
        }

        private IEnumerable<MachineResource> FilterByEnvironments(List<EnvironmentResource> environmentResources)
        {
            var environmentsToInclude = environmentResources.Where(e => environments.Contains(e.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            var missingEnvironments = environments.Except(environmentsToInclude.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToList();
            if (missingEnvironments.Any())
                throw new CouldNotFindException("environment(s) named", string.Join(", ", missingEnvironments));


            var environmentFilter = environmentsToInclude.Select(p => p.Id).ToList();

            Log.Debug("Loading machines...");
            IEnumerable<MachineResource> environmentMachines;
            if (environmentFilter.Count > 0)
            {
                Log.Debug("Loading machines from {0}...", string.Join(", ", environmentsToInclude.Select(e => e.Name)));
                environmentMachines =
                    Repository.Machines.FindMany(
                        x => { return x.EnvironmentIds.Any(environmentId => environmentFilter.Contains(environmentId)); });
            }
            else
            {
                Log.Debug("Loading machines from all environments...");
                environmentMachines = Repository.Machines.FindAll();
            }
            return environmentMachines;
        }
    }
}
