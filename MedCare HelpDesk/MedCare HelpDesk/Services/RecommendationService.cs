using MedCare_HelpDesk.Models.ML;
using MedCare_HelpDesk.Repositories.Interfaces;
using MedCare_HelpDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

namespace MedCare_HelpDesk.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly IRepositoryWrapper _repository;
        private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "but", "is", "are", "was", "were", "be", "been", "being",
            "to", "of", "in", "on", "for", "with", "at", "by", "from", "up", "about", "into", "over",
            "after", "my", "i", "me", "we", "our", "you", "your", "it", "its", "this", "that", "these",
            "those", "not", "can", "cannot", "cant", "wont", "please", "help", "issue", "issues",
            "problem", "problems", "error", "errors", "need", "needs", "working", "work", "works",
            "tried", "trying", "having", "have", "has", "had", "also", "just", "get", "getting",
            "when", "how", "what", "where", "which", "who", "whom", "there", "here", "am", "do",
            "does", "did", "doing", "would", "should", "could", "ward", "hospital", "medcare"
        };

        public RecommendationService(IRepositoryWrapper repository)
        {
            _repository = repository;
        }

        public async Task<List<ArticleRecommendationDto>> GetRecommendedArticlesAsync(string text, int topK = 3)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3)
                return new List<ArticleRecommendationDto>();

            var articles = await _repository.KnowledgeBase.FindAll().ToListAsync();
            if (!articles.Any())
                return new List<ArticleRecommendationDto>();

            var corpus = new List<string> { text };
            corpus.AddRange(articles.Select(a =>
            {
                string plainContent = StripHtml(a.Content ?? "");
                return $"{a.Title} {a.Title} {a.Title} {a.Title} {a.Category} {a.Category} {plainContent}";
            }));

            var tfidfVectors = ComputeTfIdfVectors(corpus);
            var queryVector = tfidfVectors[0];
            var queryTokens = TokenizeAndClean(text).ToHashSet();

            var results = new List<ArticleRecommendationDto>();
            for (int i = 0; i < articles.Count; i++)
            {
                double sim = CalculateCosineSimilarity(queryVector, tfidfVectors[i + 1]);
                var titleTokens = TokenizeAndClean($"{articles[i].Title} {articles[i].Category}").ToHashSet();
                bool hasTitleOverlap = queryTokens.Any(qt => titleTokens.Any(tt => tt == qt || (qt.Length >= 4 && tt.StartsWith(qt))));

                if (sim >= 0.06 || (hasTitleOverlap && sim > 0.02))
                {
                    double boostedPct = Math.Min(99.0, Math.Round((sim + (hasTitleOverlap ? 0.12 : 0.0)) * 100, 1));
                    results.Add(new ArticleRecommendationDto
                    {
                        ArticleId = articles[i].Id,
                        Title = articles[i].Title ?? "Untitled Guide",
                        Category = articles[i].Category ?? "Medical Equipment",
                        Similarity = boostedPct,
                        Content = articles[i].Content ?? ""
                    });
                }
            }

            return results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
        }

        public async Task<List<SimilarTicketDto>> GetSimilarResolvedTicketsAsync(int ticketId, int topK = 3, int? requesterId = null, int? wardId = null)
        {
            var allTickets = await _repository.Ticket.GetQueryableTicketsWithDetails().ToListAsync();
            var currentTicket = allTickets.FirstOrDefault(t => t.Id == ticketId);
            if (currentTicket == null)
                return new List<SimilarTicketDto>();

            var resolvedTicketsQuery = allTickets
                .Where(t => t.Id != ticketId && (t.Status == "Resolved" || t.Status == "Closed"));

            if (requesterId.HasValue && wardId.HasValue)
            {
                resolvedTicketsQuery = resolvedTicketsQuery.Where(t =>
                    t.RequesterId == requesterId.Value ||
                    t.DepartmentId == wardId.Value ||
                    (t.Requester != null && t.Requester.DepartmentId == wardId.Value));
            }
            else if (requesterId.HasValue)
            {
                resolvedTicketsQuery = resolvedTicketsQuery.Where(t => t.RequesterId == requesterId.Value);
            }

            var resolvedTickets = resolvedTicketsQuery.ToList();
            if (!resolvedTickets.Any())
                return new List<SimilarTicketDto>();

            var corpus = new List<string> { $"{currentTicket.Title} {currentTicket.Title} {currentTicket.EquipmentTag} {currentTicket.Description}" };
            corpus.AddRange(resolvedTickets.Select(t => $"{t.Title} {t.Title} {t.EquipmentTag} {t.Description}"));

            var tfidfVectors = ComputeTfIdfVectors(corpus);
            var queryVector = tfidfVectors[0];

            var results = new List<SimilarTicketDto>();
            for (int i = 0; i < resolvedTickets.Count; i++)
            {
                double sim = CalculateCosineSimilarity(queryVector, tfidfVectors[i + 1]);
                if (sim >= 0.15)
                {
                    results.Add(new SimilarTicketDto
                    {
                        TicketId = resolvedTickets[i].Id,
                        TicketCode = resolvedTickets[i].TicketCode ?? $"#{resolvedTickets[i].Id}",
                        Title = resolvedTickets[i].Title ?? "Untitled Ticket",
                        Status = resolvedTickets[i].Status ?? "Resolved",
                        Similarity = Math.Round(sim * 100, 1)
                    });
                }
            }

            return results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            string noTags = Regex.Replace(html, "<[^>]+>", " ");
            return WebUtility.HtmlDecode(noTags);
        }

        private static string NormalizeToken(string token)
        {
            if (token == "wifi" || token == "wi" || token == "fi" || token == "wlan") return "wifi";
            if (token == "frozen" || token == "freezes" || token == "freezing") return "freeze";
            if (token == "crashed" || token == "crashes" || token == "crashing") return "crash";
            if (token == "jammed" || token == "jamming" || token == "jams") return "jam";
            if (token.Length > 4 && token.EndsWith("ies")) return token[..^3] + "y";
            if (token.Length > 5 && token.EndsWith("ing")) return token[..^3];
            if (token.Length > 4 && token.EndsWith("ed")) return token[..^2];
            if (token.Length > 4 && token.EndsWith("es")) return token[..^2];
            if (token.Length > 3 && token.EndsWith('s') && !token.EndsWith("ss") && !token.EndsWith("us") && token != "pacs" && token != "his" && token != "lis")
                return token[..^1];
            return token;
        }

        private List<string> TokenizeAndClean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            string normalizedText = StripHtml(text)
                .ToLowerInvariant()
                .Replace("wi-fi", "wifi");

            return Regex.Matches(normalizedText, @"[a-z0-9]+")
                .Select(m => NormalizeToken(m.Value))
                .Where(token => token.Length > 1 && !_stopWords.Contains(token))
                .ToList();
        }

        private double[][] ComputeTfIdfVectors(List<string> corpus)
        {
            var tokenizedDocs = corpus.Select(TokenizeAndClean).ToList();
            var vocabulary = tokenizedDocs.SelectMany(d => d).Distinct().ToList();
            int docCount = corpus.Count;
            int vocabSize = vocabulary.Count;

            if (vocabSize == 0)
                return new double[docCount][];

            var idf = new double[vocabSize];
            for (int v = 0; v < vocabSize; v++)
            {
                string term = vocabulary[v];
                int docsWithTerm = tokenizedDocs.Count(d => d.Contains(term));
                idf[v] = Math.Log(1.0 + (double)docCount / (1.0 + docsWithTerm));
            }

            var vectors = new double[docCount][];
            for (int d = 0; d < docCount; d++)
            {
                vectors[d] = new double[vocabSize];
                var docTokens = tokenizedDocs[d];
                if (docTokens.Count == 0) continue;

                for (int v = 0; v < vocabSize; v++)
                {
                    string term = vocabulary[v];
                    int termCount = docTokens.Count(t => t == term);
                    if (termCount > 0)
                    {
                        double tf = (double)termCount / docTokens.Count;
                        vectors[d][v] = tf * idf[v];
                    }
                }
            }

            return vectors;
        }

        public static double CalculateCosineSimilarity(double[] vecA, double[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length == 0 || vecB.Length == 0 || vecA.Length != vecB.Length)
                return 0.0;

            double dotProduct = 0.0, normA = 0.0, normB = 0.0;
            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            return (normA == 0.0 || normB == 0.0) ? 0.0 : dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
