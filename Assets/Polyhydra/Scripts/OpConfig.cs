// Copyright 2022 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using Polyhydra.Core;

public class OpConfig
{
    public bool usesAmount = true;
    public float amountDefault = 0;
    public float amountMin = -20;
    public float amountMax = 20;
    public float amountSafeMin = -10;
    public float amountSafeMax = 0.999f;
    public bool usesAmount2 = false;
    public float amount2Default = 0;
    public float amount2Min = -20;
    public float amount2Max = 20;
    public float amount2SafeMin = -10;
    public float amount2SafeMax = 0.999f;
    public bool usesFilter = false;
    public bool usesColor = false;
    public string colorDefault;
    public int[,] matrix;
}

public static class OpConfigs
{
    public static (int v, int e, int f) CalcVef(PolyMesh poly, PolyMesh.Operation op)
    {
	    var matrix = Configs[op].matrix;
	    int v = poly.Vertices.Count;
	    int e = poly.EdgeCount;
	    int f = poly.Faces.Count;
	    return (
		    (matrix[0, 0] * v) + (matrix[0, 1] * e) + (matrix[0, 2] * f),
		    (matrix[1, 0] * v) + (matrix[1, 1] * e) + (matrix[1, 2] * f),
		    (matrix[2, 0] * v) + (matrix[2, 1] * e) + (matrix[2, 2] * f)
	    );
    }

    public static readonly Dictionary<PolyMesh.Operation, OpConfig> Configs = new Dictionary<PolyMesh.Operation, OpConfig>
    {
		{PolyMesh.Operation.Identity, new OpConfig
		{
			usesAmount = false,
			matrix = new [,]{{1,0,0},{0,1,0},{0,0,1}}
		}},
		{
			PolyMesh.Operation.Kis,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.1f,
				amountMin = -6, amountMax = 6, amountSafeMin = -0.5f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,0,1},{0,3,0},{0,2,0}}
			}
		},
		{PolyMesh.Operation.Dual, new OpConfig
		{
			usesAmount = false,
			matrix = new [,]{{0,0,1},{0,1,0},{1,0,0}}
		}},
		{PolyMesh.Operation.Ambo, new OpConfig
		{
			usesAmount = false,
			matrix = new [,]{{0,1,0},{0,2,0},{1,0,1}}
		}},
		{
			PolyMesh.Operation.Zip,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -2f, amountMax = 2f, amountSafeMin = 0.0001f, amountSafeMax = .999f,
				matrix = new [,]{{0,2,0},{0,3,0},{1,0,1}}
			}
		},
		{
			PolyMesh.Operation.Expand,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{0,2,0},{0,4,0},{1,1,1}}
			}
		},
		{
			PolyMesh.Operation.Bevel,
			new OpConfig
			{
				amountDefault = 0.25f,
				amountMin = -6, amountMax = 6, amountSafeMin = 0.001f, amountSafeMax = 0.4999f,
				usesAmount2 = true,
				amount2Default = 0.25f,
				amount2Min = -6, amount2Max = 6, amount2SafeMin = 0.001f, amount2SafeMax = 0.4999f,
				matrix = new [,]{{0,4,0},{0,6,0},{1,1,1}}

			}
		},
		{
			PolyMesh.Operation.Join,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -1f, amountMax = 2f, amountSafeMin = -0.5f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,0,1},{0,2,0},{0,1,0}}
			}
		},
		{ // TODO Support random
			PolyMesh.Operation.Needle,
			new OpConfig
			{
				amountDefault = 0f,
				amountMin = -6, amountMax = 6, amountSafeMin = -0.5f, amountSafeMax = 0.5f,
				matrix = new [,]{{1,0,1},{0,3,0},{0,2,0}}
}
		},
		{
			PolyMesh.Operation.Ortho,
			new OpConfig
			{
				amountDefault = 0.1f,
				amountMin = -6, amountMax = 6, amountSafeMin = -0.5f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,1,1},{0,4,0},{0,2,0}}
			}
		},
		{
			PolyMesh.Operation.Meta,
			new OpConfig
			{
				amountDefault = 0f,
				amountMin = -6, amountMax = 6, amountSafeMin = -0.333f, amountSafeMax = 0.666f,
				usesAmount2 = true,
				amount2Default = 0f,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 0.99f,
				matrix = new [,]{{1,1,1},{0,6,0},{0,4,0}}

			}
		},
		{
			PolyMesh.Operation.Truncate,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.3f,
				amountMin = -6, amountMax = 6, amountSafeMin = 0.001f, amountSafeMax = 0.499f,
				matrix = new [,]{{0,2,0},{0,3,0},{1,0,1}},
			}
		},
		{
			PolyMesh.Operation.Gyro,
			new OpConfig
			{
				amountDefault = 0.33f,
				amountMin = -.5f, amountMax = 0.5f, amountSafeMin = 0.001f, amountSafeMax = 0.499f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1.0f,
				matrix = new [,]{{1,2,1},{0,5,0},{0,2,0}}
			}
		},
		{
			PolyMesh.Operation.Snub,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -1f, amountMax = 1f, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{0,2,0},{0,5,0},{1,2,1}}
			}
		},
		{PolyMesh.Operation.Subdivide, new OpConfig
		{
			amountDefault = 0,
			amountMin = -3, amountMax = 3, amountSafeMin = -0.5f, amountSafeMax = 1,
			matrix = new [,]{{0,1,1},{0,4,0},{1,2,0}}
		}},
		{
			PolyMesh.Operation.Loft,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -1, amount2SafeMax = 1,
				matrix = new [,]{{1,2,0},{0,5,0},{0,2,1}}
			}
		},
		{
			PolyMesh.Operation.Chamfer,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,2,0},{0,4,0},{0,1,1}}
			}
		},
		{
			PolyMesh.Operation.Quinto,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,3,0},{0,6,0},{0,2,1}}
			}
		},
		{
			PolyMesh.Operation.Lace,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,0},{0,7,0},{0,4,1}}
			}
		},
		{
			PolyMesh.Operation.JoinedLace,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,0},{0,6,0},{0,3,1}}
			}
		},
		{
			PolyMesh.Operation.OppositeLace,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,0},{0,7,0},{0,4,1}}
			}
		},
		{
			PolyMesh.Operation.JoinKisKis,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,1},{0,8,0},{0,5,0}}
			}
		},
		{
			PolyMesh.Operation.Stake,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f, amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,2,1},{0,7,0},{0,4,0}}
			}
		},
		{
			PolyMesh.Operation.JoinStake,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,2,1},{0,6,0},{0,3,0}}
			}
		},
		{
			PolyMesh.Operation.Medial,
			new OpConfig
			{
				amountDefault = 2f,
				amountMin = 2, amountMax = 8, amountSafeMin = 1, amountSafeMax = 6,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,1},{0,7,0},{0,4,0}}  // Only valid for n=1
			}
		},
		{
			PolyMesh.Operation.EdgeMedial,
			new OpConfig
			{
				amountDefault = 2f,
				amountMin = 2, amountMax = 8, amountSafeMin = 1, amountSafeMax = 6,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
				matrix = new [,]{{1,2,1},{1,7,0},{1,4,0}}  // Only valid for n=1
			}
		},
		// {
		// 	PolyMesh.Operation.JoinedMedial,
		// 	new OpConfig
		// 	{
		// 		amountDefault=2f,
		// 		amountMin=2, amountMax=8, amountSafeMin=1, amountSafeMax=4,
		// 		usesAmount2 = true,
		// 		amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1,
		// 	}
		// },
		{
			PolyMesh.Operation.Propeller,
			new OpConfig
			{
				amountDefault = 0.25f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0f, amountSafeMax = 0.5f,
				matrix = new [,]{{1,2,0},{0,5,0},{0,2,1}}
			}
		},
		{
			PolyMesh.Operation.Whirl,
			new OpConfig
			{
				amountDefault = 0.25f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.5f,
				matrix = new [,]{{1,4,0},{0,7,0},{0,2,1}}
			}
		},
		{
			PolyMesh.Operation.Volute,
			new OpConfig
			{
				amountDefault = 0.33f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{0,2,1},{0,7,0},{1,4,0}}
			}
		},
		{
			PolyMesh.Operation.Exalt,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.1f,
				amountMin = -6, amountMax = 6, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,2,1},{0,9,0},{3,4,0}}
			}
		},
		{
			PolyMesh.Operation.Yank,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.33f,
				amountMin = -6, amountMax = 6, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{3,4,0},{0,9,0},{1,2,1}}
			}
		},
		{
			PolyMesh.Operation.Cross,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -1, amountMax = 1, amountSafeMin = -1, amountSafeMax = 0.999f,
				matrix = new [,]{{1,3,1},{0,10,0},{0,6,0}}
			}
		},

		{
			PolyMesh.Operation.Squall,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{1,3,0},{0,8,0},{3,0,5}}
				// What's this?
				// matrix = new [,]{{0,3,1},{0,8,0},{1,4,0}}
			}
		},
		{
			PolyMesh.Operation.JoinSquall,
			new OpConfig
			{
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				matrix = new [,]{{0,3,0},{0,6,0},{1,2,1}}
			}
		},
		{
			PolyMesh.Operation.SplitFaces,
			new OpConfig
			{
				usesFilter = true,
				usesAmount = false,
			}
		},
		{
			PolyMesh.Operation.Gable,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = 0.001f, amountSafeMax = 0.999f,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 3, amount2SafeMin = -0.5f, amount2SafeMax = 1
			}
		},
		{
			PolyMesh.Operation.FaceOffset,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.1f,
				amountMin = -6, amountMax = 6, amountSafeMin = -1, amountSafeMax = 0.999f
			}
		},
		//{PolyMesh.Operation.Ribbon, new OpConfig{}},
		{
			PolyMesh.Operation.Extrude,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.1f,
				amountMin = -12, amountMax = 12, amountSafeMin = -6f, amountSafeMax = 6f
			}
		},
		{
			PolyMesh.Operation.Shell,
			new OpConfig
			{
				amountDefault = 0.1f,
				amountMin = -6, amountMax = 6, amountSafeMin = 0.001f, amountSafeMax = 0.999f
			}
		},
		{
			PolyMesh.Operation.Skeleton,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.1f,
				amountMin = -6, amountSafeMin = 0.001f, amountSafeMax = 0.999f, amountMax = 6
			}
		},
		{
			PolyMesh.Operation.Segment,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -3, amountSafeMin = 0, amountSafeMax = 1, amountMax = 3,
				usesAmount2 = true,
				amount2Min = -3, amount2Max = 0, amount2SafeMin = 1f, amount2SafeMax = 3,

			}
		},
		{
			PolyMesh.Operation.VertexScale,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -6, amountMax = 6, amountSafeMin = -1, amountSafeMax = 0.999f
			}
		},
		{
			PolyMesh.Operation.VertexRotate,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = -1, amountSafeMax = 1
			}
		},
		// {
		// 	PolyMesh.Operation.VertexFlex,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0.1f, amountMin = -6, amountMax = 6, amountSafeMin = -1, amountSafeMax = 0.999f
		// 	}
		// },
		{
			PolyMesh.Operation.VertexStellate,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = -0.5f,
				amountMin = -6, amountMax = 6, amountSafeMin = -1, amountSafeMax = 0
			}
		},
		//{PolyMesh.Operation.FaceTranslate, new OpConfig{usesFaces=true, amountDefault=0.1f, amountMin=-6, amountMax=6}},
		{
			PolyMesh.Operation.FaceScale,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = -0.5f,
				amountMin = -6, amountMax = 6, amountSafeMin = -1, amountSafeMax = 0
			}
		},
		{
			PolyMesh.Operation.FaceRotate,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 45f,
				amountMin = -3, amountMax = 3, amountSafeMin = -1, amountSafeMax = 1
			}
		},
		{
			PolyMesh.Operation.FaceRotateX,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 45f,
				amountMin = -3, amountMax = 3, amountSafeMin = -1, amountSafeMax = 1
			}
		},
		{
			PolyMesh.Operation.FaceRotateY,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 45f,
				amountMin = -3, amountMax = 3, amountSafeMin = -1, amountSafeMax = 1
			}
		},
		// {
		// 	PolyMesh.Operation.PolarOffset,
		// 	new OpConfig
		// 	{
		// 		usesFaces = true,
		// 		amountDefault = 0.5f,
		// 		amountMin = -4, amountMax = 4, amountSafeMin = -1, amountSafeMax = 1,
		// 	}
		// },
		{
			PolyMesh.Operation.FaceSlide,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 0.5f,
				amountMin = -4, amountMax = 4, amountSafeMin = -1f, amountSafeMax = 1f,
				usesAmount2 = true,
				amount2Min = -4f, amount2Max = 4f, amount2SafeMin = -2f, amount2SafeMax = 2f
			}
		},
//			{PolyMesh.Operation.FaceRotateX, new OpConfig{usesFaces=true, amountDefault=0.1f, amountMin=-180, amountMax=180}},
//			{PolyMesh.Operation.FaceRotateY, new OpConfig{usesFaces=true, amountDefault=0.1f, amountMin=-180, amountMax=180}},
		{PolyMesh.Operation.FaceRemove, new OpConfig {usesFilter = true, usesAmount = false}},
		// {PolyMesh.Operation.FaceRemoveX, new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -1, amountMax = 1, amountSafeMin = -1f, amountSafeMax = 1f,
		// 		usesAmount2 = true,
		// 		amount2Min = -1, amount2Max = 1, amount2SafeMin = -1f, amount2SafeMax = 1,
		// 	}
		// },
		// {PolyMesh.Operation.FaceRemoveY, new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -1, amountMax = 1, amountSafeMin = -1f, amountSafeMax = 1f,
		// 		usesAmount2 = true,
		// 		amount2Min = -1, amount2Max = 1, amount2SafeMin = -1f, amount2SafeMax = 1,
		// 	}
		// },
		// {PolyMesh.Operation.FaceRemoveZ, new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -1, amountMax = 1, amountSafeMin = -1f, amountSafeMax = 1f,
		// 		usesAmount2 = true,
		// 		amount2Min = -1, amount2Max = 1, amount2SafeMin = -1f, amount2SafeMax = 1,
		// 	}
		// },
		// {PolyMesh.Operation.FaceRemoveDistance, new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -1, amountMax = 1, amountSafeMin = -1f, amountSafeMax = 1f,
		// 		usesAmount2 = true,
		// 		amount2Min = -1, amount2Max = 1, amount2SafeMin = -1f, amount2SafeMax = 1,
		// 	}
		// },
		// {PolyMesh.Operation.FaceRemovePolar, new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -360, amountMax = 360, amountSafeMin = -360f, amountSafeMax = 360f,
		// 		usesAmount2 = true,
		// 		amount2Min = -360, amount2Max = 360, amount2SafeMin = -360f, amount2SafeMax = 360,
		// 	}
		// },
		{PolyMesh.Operation.FillHoles, new OpConfig {usesAmount = false}},
		// {
		// 	PolyMesh.Operation.ExtendBoundaries,
		// 	new OpConfig
		// 	{
		// 		amountDefault = 0.5f,
		// 		amountMin = -4, amountMax = 4, amountSafeMin = -1f, amountSafeMax = 1f,
		// 		usesAmount2 = true,
		// 		amount2Min = -180, amount2Max = 180, amount2SafeMin = -100f, amount2SafeMax = 100,
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.ConnectFaces,
		// 	new OpConfig
		// 	{
		// 		amountDefault = 0f,
		// 		amountMin = 0, amountMax = 1, amountSafeMin = 0, amountSafeMax = 1,
		// 		usesAmount2 = true,
		// 		amount2Min = 0, amount2Max = 1, amount2SafeMin = 0, amount2SafeMax = 1,
		// 	}
		// },
		// {PolyMesh.Operation.FaceMerge, new OpConfig {usesFilter = true, usesAmount = false}},
		{PolyMesh.Operation.FaceKeep, new OpConfig {usesFilter = true, usesAmount = false}},
		{PolyMesh.Operation.VertexRemove, new OpConfig {usesFilter = true, usesAmount = false}},
		{PolyMesh.Operation.VertexKeep, new OpConfig {usesFilter = true, usesAmount = false}},
		// {
		// 	PolyMesh.Operation.Layer,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0.1f,
		// 		amountMin = -2f, amountMax = 2f, amountSafeMin = -2f, amountSafeMax = 2f,
		// 		usesAmount2 = true,
		// 		amount2Min = -3, amount2Max = 3, amount2SafeMin = -1, amount2SafeMax = 1
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.Hinge,
		// 	new OpConfig
		// 	{
		// 		amountDefault = 15f,
		// 		amountMin = -180, amountMax = 180, amountSafeMin = 0, amountSafeMax = 180
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddDual,
		// 	new OpConfig
		// 	{
		// 		amountDefault = 1f,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddCopyX,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddCopyY,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddCopyZ,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddMirrorX,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddMirrorY,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.AddMirrorZ,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2
		// 	}
		// },
		// {PolyMesh.Operation.Stash, new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		usesAmount = false
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.Unstash,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2,
		// 		usesAmount2 = true,
		// 		amount2Min = -6, amount2Max = 6, amount2SafeMin = -2, amount2SafeMax = 2
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.UnstashToFaces,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2,
		// 		usesAmount2 = true,
		// 		amount2Min = -3, amount2Max = 3, amount2SafeMin = -1, amount2SafeMax = 1
		// 	}
		// },
		// {
		// 	PolyMesh.Operation.UnstashToVerts,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0,
		// 		amountMin = -6, amountMax = 6, amountSafeMin = -2, amountSafeMax = 2,
		// 		usesAmount2 = true,
		// 		amount2Min = -3, amount2Max = 3, amount2SafeMin = -1, amount2SafeMax = 1
		// 	}
		// },
		{PolyMesh.Operation.AddTag, new OpConfig
			{
				usesFilter = true,
                usesAmount = false,
                usesColor = true,
                colorDefault = "#FFFFFF",
			}
		},
        {PolyMesh.Operation.RemoveTag, new OpConfig
            {
                usesFilter = true,
                usesAmount = false,
                usesColor = true,
                colorDefault = "#FFFFFF",
            }
        },
        {PolyMesh.Operation.ClearTags, new OpConfig
            {
                usesFilter = true,
                usesAmount = false,
            }
        },
		// {
		// 	PolyMesh.Operation.Stack,
		// 	new OpConfig
		// 	{
		// 		usesFilter = true,
		// 		amountDefault = 0.5f,
		// 		amountMin = -2f, amountMax = 2f, amountSafeMin = -2f, amountSafeMax = 2f,
		// 		usesAmount2 = true,
		// 		amount2Default =  0.8f,
		// 		amount2Min = 0.1f, amount2Max = .9f, amount2SafeMin = .01f, amount2SafeMax = .99f
		// 	}
		// },
		{
			PolyMesh.Operation.Canonicalize,
			new OpConfig
			{
				usesAmount = false,
			}
		},
		{
			PolyMesh.Operation.ConvexHull,
			new OpConfig
			{
				usesFilter = false,
				usesAmount = false,
			}
		},
		{
			PolyMesh.Operation.Spherize,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 1.0f, amountMin = -2, amountMax = 2, amountSafeMin = -2,
				amountSafeMax = 2f
			}
		},
		{
			PolyMesh.Operation.Cylinderize,
			new OpConfig
			{
				usesFilter = true,
				amountDefault = 1.0f, amountMin = -2, amountMax = 2, amountSafeMin = -2,
				amountSafeMax = 2f
			}
		},
		// {
		// 	PolyMesh.Operation.Stretch,
		// 	new OpConfig
		// 	{
		// 		amountDefault = 1.0f,
		// 		amountMin = -6f, amountMax = 6f, amountSafeMin = -3f, amountSafeMax = 3f
		// 	}
		// },
		{PolyMesh.Operation.Recenter, new OpConfig {usesAmount = false}},
		{PolyMesh.Operation.SitLevel, new OpConfig
		{
			amountDefault = 0,
			amountMin = 0f, amountMax = 1f, amountSafeMin = 0f, amountSafeMax = 1f,
		}},
		{
			PolyMesh.Operation.Weld,
			new OpConfig
			{
				amountDefault = 0.001f,
				amountMin = 0, amountMax = .25f, amountSafeMin = 0.001f, amountSafeMax = 0.1f
			}
		}
	};
	
}