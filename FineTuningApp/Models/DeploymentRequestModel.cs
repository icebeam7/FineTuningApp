namespace FineTuningApp.Models
{
    public class DeploymentRequestModel
    {
        public Sku sku { get; set; }
        public Properties properties { get; set; }
    }

    public class Sku
    {
        public string name { get; set; } = "Standard";
        public int capacity { get; set; } = 1;
    }

    public class Properties
    {
        public Model model { get; set; }
        public string provisioningState { get; set; }
    }

    public class Model
    {
        public string format { get; set; } = "OpenAI";
        public string name { get; set; }
        public string version { get; set; } = "1";
    }
}
