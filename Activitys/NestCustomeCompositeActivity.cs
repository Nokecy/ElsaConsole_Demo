using Elsa.Expressions.Models;
using Elsa.Workflows.Activities;

namespace ElsaConsole.Activitys
{
    public class NestCustomeCompositeActivity : Composite
    {
        public NestCustomeCompositeActivity()
        {
            var javascriptExp = new Expression("JavaScript", "getCustomer().Name");

            this.Root = new Sequence()
            {
                Activities =
                {
                    new WriteLine("Nest---TEST"),

                    new WriteLine("Nest---TEST1"),

                    //new WriteLine(javascriptExp),
                }
            };
        }
    }
}