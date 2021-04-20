namespace SVGMeshUnity.Internals.Cdt2d
{
    public class Triangulation
    {
        // https://github.com/mikolalysenko/cdt2d
        
        public bool Delaunay = true;
        public bool Interior = true;
        public bool Exterior = true;
        public bool Infinity = false;

        public WorkBufferPool WorkBufferPool;

        private MonotoneTriangulation MonotoneTriangulation = new MonotoneTriangulation();
        private DelaunayRefine DelaunayRefine = new DelaunayRefine();
        private Filter Filter = new Filter();

        public void BuildTriangles(MeshData data)
        {
            //Handle trivial case
            if ((!Interior && !Exterior) || data.Vertices.Count == 0)
            {
                return;
            }

            //Construct initial triangulation
            MonotoneTriangulation.WorkBufferPool = WorkBufferPool;
            MonotoneTriangulation.BuildTriangles(data);

            //If delaunay refinement needed, then improve quality by edge flipping
            if (Delaunay || Interior != Exterior || Infinity)
            {
                //Index all of the cells to support fast neighborhood queries
                var triangles = new Triangles(data);

                //Run edge flipping
                if (Delaunay)
                {
                    DelaunayRefine.WorkBufferPool = WorkBufferPool;
                    DelaunayRefine.RefineTriangles(triangles);
                }

                Filter.WorkBufferPool = WorkBufferPool;
                Filter.Infinity = Infinity;

                //Filter points
                if (!Exterior)
                {
                    Filter.Target = -1;
                    Filter.Do(triangles, data.Triangles);
                    return;
                }
                if (!Interior)
                {
                    Filter.Target = 1;
                    Filter.Do(triangles, data.Triangles);
                    return;
                }
                if (Infinity)
                {
                    Filter.Target = 0;
                    Filter.Do(triangles, data.Triangles);
                    return;
                }
                
                triangles.Fill(data.Triangles);

            }
        }

    }
}