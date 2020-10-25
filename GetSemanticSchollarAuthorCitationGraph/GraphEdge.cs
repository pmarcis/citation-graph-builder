using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetSemanticScholarAuthorCitationGraph
{
    public class GraphEdge
    {
        public int id;
        public int from;
        public int to;
        public int weight;
        public GraphColor color;
        public GraphEdge()
        {

        }
    }
}
