namespace FineTuningApp.Models
{
    internal class TrainingResponseModel
    {
        public string status { get; set; }
        public string id { get; set; }
        public string? fine_tuned_model { get; set; }
    }
}
