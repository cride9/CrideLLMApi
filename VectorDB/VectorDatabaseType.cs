using System;
using System.Collections.Generic;
using System.Text;

namespace CrideLLMApi.VectorDB;

public enum VectorDatabaseType
{
    NONE,
    CHROMADB,
    QDRANT,
    COUNT
}
