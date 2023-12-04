namespace FineTuningApp.Models
{
    public class TrainingRequestModel
    {
        public string model { get; set; }
        public string training_file { get; set; }
        public string validation_file { get; set; }

    }
}
