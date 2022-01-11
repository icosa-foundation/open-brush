using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SVGMeshUnity.Internals.Cdt2d
{
    public class Filter
    {
        public int Target;
        public bool Infinity;
        
        public WorkBufferPool WorkBufferPool;

        public void Do(Triangles triangles, List<int> result)
        {
            using (var index = IndexCells(triangles))
            {
                var cellN = index.Cells.UsedSize;
                
                if (result.Capacity < cellN * 3)
                {
                    result.Capacity = cellN * 3;
                }
                
                result.Clear();
                
                if (Target == 0)
                {
                    if (Infinity)
                    {
                        FillTriangles(index.Cells, result);
                        FillTriangles(index.Boundary, result);
                        return;
                    }
                    else
                    {
                        FillTriangles(index.Cells, result);
                        return;
                    }
                }

                var side = 1;
                var active = index.Active;
                var next = index.Next;
                var flags = index.Flags;
                var flagsData = flags.Data;
                var cells = index.Cells;
                var constraint = index.Constraint;
                var constraintData = constraint.Data;
                var neighbor = index.Neighbor;
                var neighborData = neighbor.Data;

                while (active.UsedSize > 0 || next.UsedSize > 0)
                {
                    while (active.UsedSize > 0)
                    {
                        var t = active.Pop();
                        if (flagsData[t] == -side)
                        {
                            continue;
                        }

                        flagsData[t] = side;
                        for (var j = 0; j < 3; ++j)
                        {
                            var f = neighborData[3 * t + j];
                            if (f >= 0 && flagsData[f] == 0)
                            {
                                if (constraintData[3 * t + j])
                                {
                                    next.Push(ref f);
                                }
                                else
                                {
                                    active.Push(ref f);
                                    flagsData[f] = side;
                                }
                            }
                        }
                    }

                    //Swap arrays and loop
                    var tmp = next;
                    next = active;
                    active = tmp;
                    next.Clear();
                    side = -side;
                }

                FilterCells(cells, flags);
                FillTriangles(cells, result);

                if (Infinity)
                {
                    FillTriangles(index.Boundary, result);
                }
            }
        }

        private class FaceIndex : IDisposable
        {
            public FaceIndex(WorkBufferPool pool)
            {
                WorkBufferPool = pool;
                pool.Get(ref Cells);
                pool.Get(ref Neighbor);
                pool.Get(ref Constraint);
                pool.Get(ref Flags);
                pool.Get(ref Active);
                pool.Get(ref Next);
                pool.Get(ref Boundary);
            }

            public WorkBuffer<Int3> Cells;
            public WorkBuffer<int> Neighbor;
            public WorkBuffer<bool> Constraint;
            public WorkBuffer<int> Flags;
            public WorkBuffer<int> Active;
            public WorkBuffer<int> Next;
            public WorkBuffer<Int3> Boundary;

            private WorkBufferPool WorkBufferPool;
            private bool Disposed = false;

            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (Disposed)
                {
                    return;
                }

                if (disposing)
                {
                    WorkBufferPool.Release(ref Boundary);
                    WorkBufferPool.Release(ref Next);
                    WorkBufferPool.Release(ref Active);
                    WorkBufferPool.Release(ref Flags);
                    WorkBufferPool.Release(ref Constraint);
                    WorkBufferPool.Release(ref Neighbor);
                    WorkBufferPool.Release(ref Cells);
                }

                Disposed = true;
            }

            public void Dump()
            {
                Debug.LogFormat("Cells:\n{0}\n", Dump(Cells));
                Debug.LogFormat("Neighbor:\n{0}\n", Dump(Neighbor));
                Debug.LogFormat("Flags:\n{0}\n", Dump(Flags));
                Debug.LogFormat("Constraint:\n{0}\n", Dump(Constraint));
                Debug.LogFormat("Active:\n{0}\n", Dump(Active));
                Debug.LogFormat("Next:\n{0}\n", Dump(Next));
                Debug.LogFormat("Boundary:\n{0}\n", Dump(Boundary));
            }

            private string Dump<T>(WorkBuffer<T> buf)
            {
                return buf.Data.Take(buf.UsedSize).Aggregate("", (_, s) => _ + " " + s.ToString() + ",\n");
            }
        }
        
        private FaceIndex IndexCells(Triangles triangles)
        {
            var zero = 0;
            var fals = false;
            
            var index = new FaceIndex(WorkBufferPool);
            
            triangles.Fill(index.Cells);
            
            //First get cells and canonicalize
            var cells = index.Cells;
            var nc = cells.UsedSize;
            var cellsData = cells.Data;
            for (var i = 0; i < nc; ++i)
            {
                var c = cellsData[i];
                var x = c.x;
                var y = c.y;
                var z = c.z;
                if (y < z)
                {
                    if (y < x)
                    {
                        c.x = y;
                        c.y = z;
                        c.z = x;
                        cellsData[i] = c;
                    }
                }
                else if (z < x)
                {
                    c.x = z;
                    c.y = x;
                    c.z = y;
                    cellsData[i] = c;
                }
            }

            WorkBuffer<Int3>.Sort(cells);

            //Initialize flag array
            var flags = index.Flags;
            flags.Fill(ref zero, nc);

            //Build neighbor index, initialize queues
            var active = index.Active;
            var next = index.Next;
            var neighbor = index.Neighbor;
            var constraint = index.Constraint;
            var boundary = index.Boundary;
            neighbor.Fill(ref zero, nc * 3);
            constraint.Fill(ref fals, nc * 3);
            var flagsData = flags.Data;
            var neighborData = neighbor.Data;
            var constraintData = constraint.Data;
            for (var i = 0; i < nc; ++i)
            {
                var c = cellsData[i];
                for (var j = 0; j < 3; ++j)
                {
                    var x = 0;
                    var y = 0;

                    switch (j)
                    {
                        case 0:
                            x = c.x;
                            y = c.y;
                            break;
                        case 1:
                            x = c.y;
                            y = c.z;
                            break;
                        case 2:
                            x = c.z;
                            y = c.x;
                            break;
                    }

                    var a = neighborData[3 * i + j] = Locate(cells, y, x, triangles.Opposite(y, x));
                    var b = constraintData[3 * i + j] = triangles.IsConstraint(x, y);
                    if (a < 0)
                    {
                        if (b)
                        {
                            next.Push(ref i);
                        }
                        else
                        {
                            active.Push(ref i);
                            flagsData[i] = 1;
                        }

                        if (Infinity)
                        {
                            var v = new Int3(y, x, -1);
                            boundary.Push(ref v);
                        }
                    }
                }
            }

            return index;
        }

        private int Locate(WorkBuffer<Int3> cells, int a, int b, int c)
        {
            var x = a;
            var y = b;
            var z = c;
            if (b < c)
            {
                if (b < a)
                {
                    x = b;
                    y = c;
                    z = a;
                }
            }
            else if (c < a)
            {
                x = c;
                y = a;
                z = b;
            }

            if (x < 0)
            {
                return -1;
            }

            return BinarySearch.EQ(cells.Data, new Int3(x, y, z), 0, cells.UsedSize - 1);
        }
        
        private void FilterCells(WorkBuffer<Int3> cells, WorkBuffer<int> flags)
        {
            var ptr = 0;
            var n = cells.UsedSize;
            var cellsData = cells.Data;
            var flagsData = flags.Data;
            for (var i = 0; i < n; ++i) {
                if(flagsData[i] == Target)
                {
                    cellsData[ptr++] = cellsData[i];
                }
            }
            cells.RemoveLast(n - ptr);
        }

        private void FillTriangles(WorkBuffer<Int3> from, List<int> to)
        {
            var n = from.UsedSize;
            
            if (to.Capacity < n * 3)
            {
                to.Capacity = n * 3;
            }

            var data = from.Data;
            for (var i = 0; i < n; ++i)
            {
                var v = data[i];
                to.Add(v.x);
                to.Add(v.y);
                to.Add(v.z);
            }
        }
    }
}