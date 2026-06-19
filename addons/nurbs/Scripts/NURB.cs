using Godot;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NURBs.Types.Matrix;
using NURBs.Types.VectorVariants.HalfVector;
using NURBs.Types.VectorVariants.ByteVector;
using NURBs.Types.VectorVariants.BitVector;
using NURBs;

using static System.Math;

namespace NURBs
{
	#region NURBClass
		public partial class NURB : MeshInstance3D
		{
			[Export]
			public float Weight = 0;

			public Vector2 CNLoc = new Vector2(0, 0);

			readonly NURBBuilder parent;

			private bool Loading = false;
			private bool QueuedForReload = false;

			#region Lambda Expressions
				List<List<Vector4>> ControlNetworkPositions => parent.ControlNetworkPositions;
				//ShaderMaterial NormalShower => parent.NormalShower;
				Camera3D LODCamera => parent.LODCamera;
				Godot.Collections.Array<Vector2> LODDistances => parent.LODDistances;
				Material SurfaceMaterial => parent.SurfaceMaterial;
				ByteVector2 CNSize => parent.CNSize;
				ByteVector2 SVMSize => parent.SVMSize;
				ByteVector2 SSize => parent.SSize;
				Matrix NB => parent.NB;
				Matrix MB => parent.MB;
				Matrix NBD => parent.NBD;
				Matrix MBD => parent.MBD;
				Matrix NPB => parent.NPB;
				Matrix MPB => parent.MPB;

				Vector2 ControlPointSpacing => parent.ControlPointSpacing;

				String BezierPrefix => parent.BezierPrefix;
			#endregion
			
			
			public NURB()
			{
				// This constructor is only used for the editor to create a preview of the surface. It should never be used for actual surfaces, and thus doesn't need to be fully initalized.
			}

			public NURB(NURBBuilder Parent, Vector2 CNLocation)
			{
				parent = Parent; // this MUST be initalized first. Other values use lambda expressions to hide the use of the parent pointer
				CNLoc = CNLocation;

				InitSelfMeshInstance();

				parent.AddChild(this, false, Node.InternalMode.Front); // Might cause issues that it's in this order.

				if (Engine.IsEditorHint())
				{
					AddTreeOwnership();
					ReloadSurface();
				}
			}
			
			public override void _Notification(int what)
			{
				if (Engine.IsEditorHint())
				{
					if (what == NotificationEditorPreSave)
					{
						SetOwner(null);
					}
					else if (what == NotificationEditorPostSave)
					{
						AddTreeOwnership();
					}
				}
			}
			
			private void AddTreeOwnership(){
				SetOwner(EditorInterface.Singleton.GetEditedSceneRoot());
			}

			public override void _Process(double delta)
			{
				if (Engine.IsEditorHint())
				{
					Godot.Collections.Array<Node> selection = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
					for (int i = 0; i < selection.Count; i++)
					{
						if (selection[i] == this)
						{
							EditorInterface.Singleton.EditNode(GetParent());
						}
					}
				}
			}
			

			public async void ReloadSurface()
			{
				if (Loading)
				{
					QueuedForReload = true;
					GD.Print("Reloading!");
					return;
				}
				else
				{
					Loading = true;
				}

				ArrayMesh arrMesh = await CreateArrayMesh();

				Godot.Collections.Array<Node> children = GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					children[i].SetOwner(null);
					children[i].QueueFree();
				}

				

				this.SetMesh(arrMesh);

				this.CreateTrimeshCollision();

				children = GetChildren();
				for (int i = 0; i < children.Count; i++)
				{
					if (children[i] is Node3D node3DChild)
					{
						node3DChild.SetOwner(null);
						node3DChild.Hide();
					}
				}


				Loading = false;
				if (QueuedForReload)
				{
					QueuedForReload = false;
					ReloadSurface();
				}
			}

			private Vector4[,] GetControlNodes()
			{
				Vector4[,] CN = new Vector4[CNSize.X, CNSize.Y];
				for (int i = 0; i + CNLoc.X < ControlNetworkPositions.Count && i < CNSize.X; i++)
				{
					for (int j = 0; j + CNLoc.Y < ControlNetworkPositions[i + (int)CNLoc.X].Count && j < CNSize.Y; j++)
					{
						CN[i, j] = ControlNetworkPositions[i + (int)CNLoc.X][j + (int)CNLoc.Y];
					}
				}
				return CN;
			}
			
			public bool IsMyControlNode(Vector2 CPLoc)
			{
				float dx = CPLoc.X - CNLoc.X;
				float dy = CPLoc.Y - CNLoc.Y;
				return ((dx >= 0) && (dy >= 0) && (dx < CNSize.X) && (dy < CNSize.Y));
			}

			private void InitSelfMeshInstance()
			{
				Name = BezierPrefix + CNLoc.X.ToString() + "_" + CNLoc.Y.ToString();

				TopLevel = true;
			}
			
			#region Create Array Mesh
			private async Task<ArrayMesh> CreateArrayMesh()
			{
				var CN = GetControlNodes();

				Vector2[,] UVs = await Task.Run(() => GetSurfaceUVs(SVMSize));
				Vector3[][,] STMandSNM = await Task.Run(() => GetSurfaceTransforms(NB, MB, NBD, MBD, NPB, MPB, CN, SVMSize));

				Vector3[,] STM = STMandSNM[0];
				Vector3[,] SNM = STMandSNM[1];

				ArrayMesh ArrMesh = new ArrayMesh();

				var SurfaceArray = new Godot.Collections.Array();

				SurfaceArray.Resize((int)Mesh.ArrayType.Max);

				SurfaceArray[(int)Mesh.ArrayType.TexUV] = WindTriangles(UVs);
				SurfaceArray[(int)Mesh.ArrayType.Vertex] = WindTriangles(STM);
				SurfaceArray[(int)Mesh.ArrayType.Normal] = WindTriangles(SNM);

				ArrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, SurfaceArray);

				if (SurfaceMaterial != null)
				{
					ArrMesh.SurfaceSetMaterial(0, SurfaceMaterial);
				}

				return ArrMesh;
			}

			#region Surface Calculations
			private static Vector2[,] GetSurfaceUVs(ByteVector2 SVMSize)
			{
				Vector2[,] UVs = new Vector2[SVMSize.X, SVMSize.Y];
				for (int u = 0; u < SVMSize.X; u++)
				{
					for (int v = 0; v < SVMSize.Y; v++)
					{
						UVs[u, v] = new Vector2(u / (SVMSize.X - 1f), v / (SVMSize.Y - 1f));
					}
				}
				return UVs;
			}

			private static Vector3[][,] GetSurfaceTransforms(Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Matrix NPB, Matrix MPB, Vector4[,] CN, ByteVector2 SVMSize)
			{

				Vector3[,] STMForklift = new Vector3[SVMSize.X, SVMSize.Y];
				Vector3[,] SNMForklift = new Vector3[SVMSize.X, SVMSize.Y];
				for (byte u = 0; u < SVMSize.X; u++)
				{
					for (byte v = 0; v < SVMSize.Y; v++)
					{
						Vector4 ForkliftT = ComputeVertexVector(u, v, NB, MB, NPB, MPB, CN, SVMSize);
						Vector4 ForkliftA = ComputeVertexVector(u, v, NBD, MB, NPB, MPB, CN, SVMSize);
						Vector4 ForkliftB = ComputeVertexVector(u, v, NB, MBD, NPB, MPB, CN, SVMSize);
						
						STMForklift[u, v] = new Vector3(ForkliftT.X / ForkliftT.W, ForkliftT.Y / ForkliftT.W, ForkliftT.Z / ForkliftT.W);

						Vector3 TangentA = new Vector3(ForkliftA.X - (ForkliftA.W * ForkliftT.X), ForkliftA.Y - (ForkliftA.W * ForkliftT.Y), ForkliftA.Z - (ForkliftA.W * ForkliftT.Z));
						Vector3 TangentB = new Vector3(ForkliftB.X - (ForkliftB.W * ForkliftT.X), ForkliftA.Y - (ForkliftB.W * ForkliftT.Y), ForkliftA.Z - (ForkliftB.W * ForkliftT.Z));

						SNMForklift[u, v] = TangentA.Cross(TangentB).Normalized();
					}
				}
				return [STMForklift, SNMForklift];
			}
		
			private static Vector4 ComputeVertexVector(byte u, byte v, Matrix NB, Matrix MB, Matrix NPB, Matrix MPB, Vector4[,] CN, ByteVector2 SVMSize)
			{
				// Calculates the transform or tangent vector of a given point on the bezier surface
				// I've shorted down many of the names so that its compact, which makes it more readable in my opinion.
				
				// u and v are the vertex on the bezier surface we are calculating.

				// NB and MB are Bernstein Polynomial matricies which lack the exponent, these are calculated in a seperate function and stored since they will almost never change.

				// NPB and MPB are the exponent part for tNB and tMB, these are also pre-calculated and stored.

				// These two lines of code are used to convert this point in u space v into a point in 0 to 1 space, since beziers are built on lerping, which happens between 0 and 1,
				// because the number is what percent of the line you have traveled on.
				// The LODedSVMSize makes the surfaces less precise at distances, that feature is currently defunct.

				float uF = (float)u / (float)(SVMSize.X - 1);
				float vF = (float)v / (float)(SVMSize.Y - 1);


				// These two lines of code raise u and v to the powers in the NPB and MPB matricies.

				Matrix pU = PowsOfI(uF, NPB).Transpose();
				Matrix pV = PowsOfI(vF, MPB);

				// Construct three different matricies so we can calculate the x, y, and z coordinates seperate.



				Matrix cNX = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNY = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNZ = new Matrix(CN.GetLength(0), CN.GetLength(1));
				Matrix cNW = new Matrix(CN.GetLength(0), CN.GetLength(1));
				for (int i = 0; i < CN.GetLength(0); i++)
				{
					for (int j = 0; j < CN.GetLength(1); j++)
					{
						cNX[i, j] = CN[i, j].X * CN[i, j].W;
						cNY[i, j] = CN[i, j].Y * CN[i, j].W;
						cNZ[i, j] = CN[i, j].Z * CN[i, j].W;
						cNW[i, j] = CN[i, j].W;
					}
				}

				// Do the main math:

				Vector4 vector = new Vector4(0, 0, 0, 0);
				
				Matrix pUProdTMB = pU.Product(MB);
				Matrix tNBProdPV = NB.Product(pV);

				vector.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0];
				vector.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0];
				vector.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0];
				vector.W = pUProdTMB.Product(cNW).Product(tNBProdPV)[0,0];

				return vector;
			}
			#endregion

			private ByteVector2 GetLODedSVMSize()
			{ // Currently defunct method for Generating Different Levels of Detail based on distance.
				return new ByteVector2(SVMSize.X, SVMSize.Y);

				/*Byte X = (byte)Math.Ceiling((float)SVMSize.x / (float)LOD);
				Byte Y = (byte)Math.Ceiling((float)SVMSize.y / (float)LOD);
				ByteVector2 LODedSVMSize = new ByteVector2(X, Y);
				return LODedSVMSize;*/
			}
			#endregion

			#region Triangles
				private T[] WindTriangles<T>(T[,] SM)
				{
					T[,] CloneSM = (T[,])SM.Clone();
					int PackRATLen = ((SVMSize.X - 1) * (SVMSize.Y - 1)) * 6;
					int n = 0;

					T[] PackRAT = new T[PackRATLen]; // Packed Reordered Array of Triangles
					for (int j = 0; j < SVMSize.Y - 1; j++)
					{
						for (int i = 0; i < SVMSize.X - 1; i++)
						{
							PackRAT[n++] = CloneSM[i, j];
							PackRAT[n++] = CloneSM[i+1, j];
							PackRAT[n++] = CloneSM[i+1, j+1];
							
							PackRAT[n++] = CloneSM[i+1, j+1];
							PackRAT[n++] = CloneSM[i, j+1];
							PackRAT[n++] = CloneSM[i, j];
						}
					}
					return PackRAT;
				}

				private static int[] GenerateTriangleLoDIndexMap(Vector3[,] TriangleArray, byte LODLevel, ByteVector2 SVMSize) // Currently Defunct function, it will be finished eventually.
				{
					// We want all the edge vertexes so that the meshes don't have holes in between them, but other than that we reduce, only taking every nth vertex, where n = LODlevel.
					int[] Lengths = GetTriangleLoDMapLengths(SVMSize, LODLevel);
					int[] TriangleLoDIndexMaps = new int[Lengths[0] + Lengths[1]];

					int j = 1;
					int[] InsideVerticies = new int[Lengths[1]];
					for (int i = 1; i < Lengths[1]; i += LODLevel)
					{
						if (i >= TriangleArray.GetLength(0) - 1)
						{
							
						}
					}

					return TriangleLoDIndexMaps;
				}

				private static int[] GetTriangleLoDMapLengths(ByteVector2 SVMSize, byte LODLevel)
				{
					int EdgeLength = (SVMSize.X * 2) + ((SVMSize.Y - 2) * 2);
					ByteVector2 NoEdgeSVMSize = new ByteVector2((byte)(SVMSize.X - 2), (byte)(SVMSize.Y - 2));
					int InsideLength = (int )(Math.Ceiling((float)NoEdgeSVMSize.X / (float)LODLevel) * (Math.Ceiling((float)NoEdgeSVMSize.Y / (float)LODLevel)));
					return [EdgeLength, InsideLength];
				}
			#endregion

			#region Pows
				static Matrix PowsOfI(float i, Matrix Pows)
				{
					Matrix PowedI = new Matrix(Pows.GetLength(0), 1);
					for (int j = 0; j < Pows.GetLength(0); j++)
					{
						PowedI[j, 0] = (float)Math.Pow((double)i, (double)Pows[j, 0]);
					}
					return PowedI;
				}
			#endregion
		}
		#endregion
}
