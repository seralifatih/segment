using System.Collections.Generic;
using Segment.App.Models;

namespace Segment.App.Services
{
    public interface ITermCollection
    {
        IEnumerable<TermEntry> FindAll();
        TermEntry? FindById(string source);
        bool Upsert(TermEntry entry);
        bool Delete(string source);
        int Count();
    }
}
