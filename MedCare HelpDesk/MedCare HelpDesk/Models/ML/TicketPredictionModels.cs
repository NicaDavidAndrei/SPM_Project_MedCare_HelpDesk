using Microsoft.ML.Data;

namespace MedCare_HelpDesk.Models.ML
{
    public class TicketTrainingData
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Dept { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }

    public class SingleFieldPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedValue { get; set; } = string.Empty;

        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class TicketPredictionOutput
    {
        public string PredictedCategory { get; set; } = string.Empty;
        public string PredictedDepartment { get; set; } = string.Empty;
        public string PredictedPriority { get; set; } = string.Empty;
        public double ConfidencePercentage { get; set; }
        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class ArticleRecommendationDto
    {
        public int ArticleId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Similarity { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class SimilarTicketDto
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }
}
