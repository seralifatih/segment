using System;
using System.Collections.Generic;
using System.Linq;
using Segment.App.Models;

namespace Segment.App.Services
{
    public class DemoDatasetService : IDemoDatasetService
    {
        public DemoDatasetPackage GetLegalDemoDataset()
        {
            return new DemoDatasetPackage
            {
                DatasetId = "legal-demo-v1",
                Name = "Legal Clauses Workshop Pack",
                Clauses = new List<DemoLegalClauseSample>
                {
                    new() { ClauseId = "c1", Title = "Confidentiality", RiskTag = "high", SourceText = "The Recipient shall keep all Confidential Information strictly confidential.", SuggestedTranslation = "Alici tum Gizli Bilgileri kesinlikle gizli tutacaktir." },
                    new() { ClauseId = "c2", Title = "Limitation of Liability", RiskTag = "high", SourceText = "In no event shall either party be liable for indirect or consequential damages.", SuggestedTranslation = "Taraflardan hicbiri dolayli veya sonucsal zararlardan sorumlu olmayacaktir." },
                    new() { ClauseId = "c3", Title = "Termination", RiskTag = "medium", SourceText = "Either party may terminate this Agreement upon thirty (30) days written notice.", SuggestedTranslation = "Taraflardan herhangi biri bu Sozlesmeyi otuz (30) gun onceden yazili bildirimle feshedebilir." },
                    new() { ClauseId = "c4", Title = "Governing Law", RiskTag = "medium", SourceText = "This Agreement shall be governed by the laws of the State of New York.", SuggestedTranslation = "Bu Sozlesme New York Eyaleti kanunlarina tabi olacaktir." },
                    new() { ClauseId = "c5", Title = "Severability", RiskTag = "low", SourceText = "If any provision is held invalid, the remaining provisions shall remain in effect.", SuggestedTranslation = "Herhangi bir hukum gecersiz sayilsa dahi kalan hukumler yururlukte kalir." }
                }
            };
        }

        public IReadOnlyList<DemoReplayFrame> BuildDeterministicReplay(int seed, int stepCount)
        {
            var dataset = GetLegalDemoDataset();
            var clauses = dataset.Clauses;
            if (stepCount <= 0 || clauses.Count == 0)
            {
                return Array.Empty<DemoReplayFrame>();
            }

            int normalizedSeed = Math.Abs(seed == int.MinValue ? 1 : seed);
            int offset = normalizedSeed % clauses.Count;
            int stride = clauses.Count == 1 ? 1 : (normalizedSeed % (clauses.Count - 1)) + 1;

            var frames = new List<DemoReplayFrame>(stepCount);
            int index = offset;

            for (int i = 0; i < stepCount; i++)
            {
                var clause = clauses[index];
                frames.Add(new DemoReplayFrame
                {
                    StepNumber = i + 1,
                    ClauseId = clause.ClauseId,
                    SourceText = clause.SourceText,
                    SuggestedTranslation = clause.SuggestedTranslation,
                    ExpectedEditHint = $"Validate terminology for {clause.Title.ToLowerInvariant()} clause."
                });

                index = (index + stride) % clauses.Count;
            }

            return frames;
        }
    }
}
