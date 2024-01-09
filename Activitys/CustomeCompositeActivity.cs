using Elsa.Expressions.Models;
using Elsa.Workflows;
using Elsa.Workflows.Activities;

namespace ElsaConsole.Activitys
{
    public class CustomeCompositeActivity : Composite
    {
        public CustomeCompositeActivity()
        {
            this.Root = new Sequence()
            {
                Activities =
                {
                    new WriteLine("TEST"),

                    new WriteLine("TEST1"),

                    new NestCustomeCompositeActivity(),
                }
            };
        }

        protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            throw new Exception("sfsdf");

            return base.ExecuteAsync(context);
        }

        protected override void OnCompleted(ActivityCompletedContext context)
        {
            base.OnCompleted(context);
        }
    }
}