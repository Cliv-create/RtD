using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtD.Models
{
    public record CacheEntry(
        long Id,
        string UpdatedAt,
        string FolderName,
        string SubType
    );
}
