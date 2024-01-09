using Elsa.Alterations.AlterationTypes;
using Elsa.Alterations.Core.Contracts;
using Elsa.Alterations.Extensions;
using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Alterations;
using Elsa.EntityFrameworkCore.Modules.Management;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Management.Contracts;
using Elsa.Workflows.Management.Entities;
using Elsa.Workflows.Management.Mappers;
using Elsa.Workflows.Management.Models;
using Elsa.Workflows.Runtime.Contracts;
using Elsa.Workflows.Runtime.Options;
using ElsaConsole.Activitys;
using Microsoft.Extensions.DependencyInjection;

async Task<WorkflowDefinition> ImportWorkflowDefinitionAsync(IServiceProvider services, string fileName)
{
    var json = await File.ReadAllTextAsync(fileName);
    var serializer = services.GetRequiredService<IActivitySerializer>();
    var model = serializer.Deserialize<WorkflowDefinitionModel>(json);

    var workflowDefinitionRequest = new SaveWorkflowDefinitionRequest
    {
        Model = model,
        Publish = true
    };

    var workflowDefinitionImporter = services.GetRequiredService<IWorkflowDefinitionImporter>();
    var result = await workflowDefinitionImporter.ImportAsync(workflowDefinitionRequest);
    return result.WorkflowDefinition;
}

// Setup service container.
var services = new ServiceCollection();

// Add Elsa services to the container.
services.AddElsa(elsa =>
{
    elsa.AddActivity<CustomeCompositeActivity>();
    elsa.AddActivity<NestCustomeCompositeActivity>();

    // Configure management feature to use EF Core.
    elsa.UseWorkflowManagement(management =>
    {
        management.UseEntityFrameworkCore(ef => ef.UseSqlite());
    });

    // Configure the default runtime feature to use EF Core.
    elsa.UseWorkflowRuntime(runtime =>
    {
        runtime.UseEntityFrameworkCore(ef => ef.UseSqlite());
    });

    // Configure management feature to use EF Core.
    elsa.UseAlterations(alteration =>
    {
        alteration.UseEntityFrameworkCore(ef =>
        {
        });
    });

    // Use Hangfire.
    elsa.UseHangfire(hangfire => hangfire.UseSqliteStorage(sqlite => sqlite.NameOrConnectionString = "elsa.sqlite.db"));

    // Use hangfire for scheduling timer events.
    elsa.UseScheduling(scheduling => scheduling.UseHangfireScheduler());
});

// Build the service container.
var serviceProvider = services.BuildServiceProvider();

// Populate registries. This is only necessary for applications  that are not using hosted services.
var registriesPopulator = serviceProvider.GetRequiredService<IRegistriesPopulator>();
await registriesPopulator.PopulateAsync();

// Import a workflow from a JSON file.
var workflowDefinition = await ImportWorkflowDefinitionAsync(serviceProvider, "workflow.json");

// Resolve a workflow runner to execute the workflow.
var workflowRunner = serviceProvider.GetRequiredService<IWorkflowRunner>();
var workflowRuntime = serviceProvider.GetRequiredService<IWorkflowRuntime>();
var _workflowInstanceStore = serviceProvider.GetRequiredService<IWorkflowInstanceStore>();
var _workflowStateMapper = serviceProvider.GetRequiredService<WorkflowStateMapper>();
var _workflowDefinitionService = serviceProvider.GetRequiredService<IWorkflowDefinitionService>();
var _alterationRunner = serviceProvider.GetRequiredService<IAlterationRunner>();

// Execute the workflow.
var runOptions = new StartWorkflowRuntimeOptions();
var result = await workflowRuntime.StartWorkflowAsync(workflowDefinition.DefinitionId, runOptions);
var workflowInstaneId = result.WorkflowInstanceId;

//await _workflowInstanceStore.SaveAsync(_workflowStateMapper.Map(result.WorkflowState)!);

// Load each workflow instance.
var workflowInstance = await _workflowInstanceStore.FindAsync(workflowInstaneId);
workflowInstance.Status = WorkflowStatus.Running;
workflowInstance.SubStatus = WorkflowSubStatus.Pending;
await _workflowInstanceStore.SaveAsync(workflowInstance);

// Setup an alteration plan.
var activityIds = workflowInstance.WorkflowState.Incidents.Select(x => x.ActivityId);
var alterations = activityIds.Select(activityId => new ScheduleActivity { ActivityId = activityId }).Cast<IAlteration>().ToList();

// Run the plan.
var results = await _alterationRunner.RunAsync(workflowInstaneId, alterations);

if (results.IsSuccessful)
{
    var workflow = await _workflowDefinitionService.MaterializeWorkflowAsync(workflowDefinition);
    var workflowExecutionContext = await WorkflowExecutionContext.CreateAsync(serviceProvider, workflow, workflowInstaneId, null, null, null, default, null);

    var options = new ResumeWorkflowRuntimeOptions
    {
    };

    //Sequence contains more than one matching element
    var resumeResult = await workflowRuntime.ResumeWorkflowAsync(workflowInstaneId, options);
}