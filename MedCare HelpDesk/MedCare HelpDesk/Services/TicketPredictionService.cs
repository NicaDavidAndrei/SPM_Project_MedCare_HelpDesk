using MedCare_HelpDesk.Models.ML;
using MedCare_HelpDesk.Repositories.Interfaces;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace MedCare_HelpDesk.Services
{
    public class TicketPredictionService : ITicketPredictionService
    {
        private readonly MLContext _mlContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private ITransformer? _trainedModelCategory;
        private ITransformer? _trainedModelDept;
        private ITransformer? _trainedModelPriority;
        private PredictionEngine<TicketTrainingData, SingleFieldPrediction>? _catEngine;
        private PredictionEngine<TicketTrainingData, SingleFieldPrediction>? _deptEngine;
        private PredictionEngine<TicketTrainingData, SingleFieldPrediction>? _prioEngine;
        private readonly object _lock = new();

        public TicketPredictionService(IServiceScopeFactory scopeFactory)
        {
            _mlContext = new MLContext(seed: 42);
            _scopeFactory = scopeFactory;
        }

        public async Task TrainModelFromDatabaseAsync()
        {
            var trainingList = GetHospitalSeedBaselineSamples();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRepositoryWrapper>();
                var dbTickets = await repo.Ticket.FindAll().ToListAsync();
                var categories = (await repo.Category.FindAll().ToListAsync())
                    .ToDictionary(c => c.Id, c => c.Name ?? "Medical Equipment");
                var departments = (await repo.Department.FindAll().ToListAsync())
                    .ToDictionary(d => d.Id, d => d.Name ?? "ATI (Intensive Care Unit)");

                foreach (var t in dbTickets)
                {
                    if (categories.TryGetValue(t.CategoryId, out var catName))
                    {
                        string deptName = (t.DepartmentId.HasValue && departments.TryGetValue(t.DepartmentId.Value, out var dName))
                            ? dName
                            : departments.Values.FirstOrDefault() ?? "ATI (Intensive Care Unit)";
                        trainingList.Add(new TicketTrainingData
                        {
                            Title = t.Title ?? "",
                            Description = t.Description ?? "",
                            Category = catName,
                            Dept = deptName,
                            Priority = string.IsNullOrEmpty(t.Priority) ? "Medium" : t.Priority
                        });
                    }
                }
            }
            catch
            {
                // Fall back gracefully to hospital baseline samples
            }

            IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingList);

            var featurePipeline = _mlContext.Transforms.Text.FeaturizeText("TitleFeats", nameof(TicketTrainingData.Title))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeats", nameof(TicketTrainingData.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeats", "DescFeats"));

            lock (_lock)
            {
                var catPipeline = featurePipeline
                    .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketTrainingData.Category)))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                _trainedModelCategory = catPipeline.Fit(dataView);

                var deptPipeline = featurePipeline
                    .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketTrainingData.Dept)))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                _trainedModelDept = deptPipeline.Fit(dataView);

                var prioPipeline = featurePipeline
                    .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketTrainingData.Priority)))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                _trainedModelPriority = prioPipeline.Fit(dataView);

                _catEngine = _mlContext.Model.CreatePredictionEngine<TicketTrainingData, SingleFieldPrediction>(_trainedModelCategory);
                _deptEngine = _mlContext.Model.CreatePredictionEngine<TicketTrainingData, SingleFieldPrediction>(_trainedModelDept);
                _prioEngine = _mlContext.Model.CreatePredictionEngine<TicketTrainingData, SingleFieldPrediction>(_trainedModelPriority);
            }
        }

        public TicketPredictionOutput PredictMetadata(string title, string desc)
        {
            if (_catEngine == null || _deptEngine == null || _prioEngine == null)
            {
                TrainModelFromDatabaseAsync().GetAwaiter().GetResult();
            }

            var sample = new TicketTrainingData
            {
                Title = title ?? "",
                Description = desc ?? ""
            };

            lock (_lock)
            {
                var catPred = _catEngine!.Predict(sample);
                var deptPred = _deptEngine!.Predict(sample);
                var prioPred = _prioEngine!.Predict(sample);

                double confidence = 85.0;
                if (catPred.Score != null && catPred.Score.Length > 0)
                {
                    float maxScore = catPred.Score.Max();
                    if (maxScore > 0f && maxScore <= 1.0f)
                    {
                        confidence = Math.Round(maxScore * 100.0, 1);
                    }
                    else
                    {
                        double sumExp = catPred.Score.Sum(s => Math.Exp(s));
                        if (sumExp > 0)
                        {
                            confidence = Math.Round((Math.Exp(maxScore) / sumExp) * 100.0, 1);
                        }
                    }
                }
                confidence = Math.Max(70.0, Math.Min(98.5, confidence));

                return new TicketPredictionOutput
                {
                    PredictedCategory = catPred.PredictedValue,
                    PredictedDepartment = deptPred.PredictedValue,
                    PredictedPriority = prioPred.PredictedValue,
                    ConfidencePercentage = confidence,
                    Score = prioPred.Score ?? Array.Empty<float>()
                };
            }
        }

        private static List<TicketTrainingData> GetHospitalSeedBaselineSamples() => new()
        {
            // Medical Equipment (Biomedical Engineering)
            new() { Title = "ATI mechanical ventilator pressure alarm fault", Description = "Bedside ICU ventilator showing persistent pressure sensor error requires urgent biomedical check", Category = "Medical Equipment", Dept = "ATI (Intensive Care Unit)", Priority = "Critical" },
            new() { Title = "Multiparameter bedside monitor ECG SpO2 freeze", Description = "Patient monitor screen frozen in ATI bay 4 no live SpO2 waveform displayed", Category = "Medical Equipment", Dept = "ATI (Intensive Care Unit)", Priority = "Critical" },
            new() { Title = "Emergency defibrillator self test failed", Description = "UPU resuscitation bay defibrillator displays calibration error code during shift check", Category = "Medical Equipment", Dept = "Emergency (UPU)", Priority = "Critical" },
            new() { Title = "Volumetric syringe infusion pump battery alert", Description = "Infusion pump power module discharges rapidly on battery backup in ICU", Category = "Medical Equipment", Dept = "ATI (Intensive Care Unit)", Priority = "High" },
            new() { Title = "Operating room surgical lighting dimmer failure", Description = "OR room 2 overhead surgical lamp flickers during procedure preparation", Category = "Medical Equipment", Dept = "Surgery (Operating Rooms)", Priority = "High" },

            // Hospital Software (Clinical IT Support)
            new() { Title = "Emergency triage HIS application freeze", Description = "Clinical Hospital Information System triage screen unresponsive cannot register arrivals", Category = "Hospital Software", Dept = "Emergency (UPU)", Priority = "Critical" },
            new() { Title = "Radiology PACS diagnostic viewer crashing", Description = "PACS CT DICOM image viewer closes unexpectedly when loading multi-slice study", Category = "Hospital Software", Dept = "Radiology & Imaging", Priority = "High" },
            new() { Title = "Lab hematology analyzer LIS interface stalled", Description = "Laboratory Information System failing to import sample results from analyzer", Category = "Hospital Software", Dept = "Clinical Laboratory", Priority = "High" },
            new() { Title = "Ward charting software slow login timeout", Description = "Nursing station EHR module logs clinicians out after 2 minutes inactivity", Category = "Hospital Software", Dept = "Cardiology", Priority = "Medium" },

            // IT / Network (Network & Infrastructure)
            new() { Title = "Emergency wristband barcode thermal printer jam", Description = "Triage thermal label printer jammed and print spooler queue stuck", Category = "IT / Network", Dept = "Emergency (UPU)", Priority = "Medium" },
            new() { Title = "Pediatrics ward Wi-Fi signal dropping repeatedly", Description = "Clinical tablet loses hospital wireless network access point connection in corridor", Category = "IT / Network", Dept = "Pediatrics", Priority = "Medium" },
            new() { Title = "Diagnostic PACS workstation Ethernet link down", Description = "Network switch port disconnected no intranet or DICOM connectivity", Category = "IT / Network", Dept = "Radiology & Imaging", Priority = "High" },
            new() { Title = "Nursing desk secondary PC keyboard mouse issue", Description = "Need replacement USB keyboard and optical mouse for ward charting desk", Category = "IT / Network", Dept = "Cardiology", Priority = "Low" },

            // Staff Requests (Facility & Access Admin)
            new() { Title = "New resident doctor account access provisioning", Description = "Create MedCare and HIS credentials for incoming cardiology staff member", Category = "Staff Requests", Dept = "Administration & Support", Priority = "Low" },
            new() { Title = "Update ward phone extension on staff profile", Description = "Clinician transferred from Surgery to ATI needs updated direct extension", Category = "Staff Requests", Dept = "Administration & Support", Priority = "Low" },
            new() { Title = "Account locked out password expired", Description = "Staff member locked out after 5 failed attempts needs password reset", Category = "Staff Requests", Dept = "Administration & Support", Priority = "Medium" },
            new() { Title = "Request new Knowledge Base troubleshooting guide", Description = "Please add illustrated 3-step checklist for thermal wristband calibration", Category = "Staff Requests", Dept = "Administration & Support", Priority = "Low" }
        };
    }
}
