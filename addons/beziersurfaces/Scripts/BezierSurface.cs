using Godot;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BezierSurfaces.Types.Matrix;
using BezierSurfaces.Types.VectorVariants.HalfVector;
using BezierSurfaces.Types.VectorVariants.ByteVector;
using BezierSurfaces.Types.VectorVariants.BitVector;
using BezierSurfaces;

using static System.Math;

namespace BezierSurfaces
{
	#region BezierSurfaceClass
		public partial class BezierSurface : MeshInstance3D
		{
			public Vector2 CNLoc = new Vector2(0, 0);

			readonly BezierSurfaceBuilder parent;

			private bool Loading = false;
			private bool QueuedForReload = false;

			#region Lambda Expressions
				List<List<Vector3>> ControlNetworkPositions => parent.ControlNetworkPositions;
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
				Matrix NPBD => parent.NPBD;
				Matrix MPBD => parent.MPBD;

				Vector2 ControlPointSpacing => parent.ControlPointSpacing;

				String BezierPrefix => parent.BezierPrefix;
			#endregion
			
			
			public BezierSurface()
			{
				// This constructor is only used for the editor to create a preview of the surface. It should never be used for actual surfaces, and thus doesn't need to be fully initalized.
			}

			public BezierSurface(BezierSurfaceBuilder Parent, Vector2 CNLocation)
			{
				parent = Parent; // this MUST be initalized first. Other values use lambda expressions to hide the use of the parent pointer
				CNLoc = CNLocation;

				InitSelfMeshInstance();

				parent.AddChild(this, false, Node.InternalMode.Front); // Might cause issues that it's in this order.

				if (Engine.IsEditorHint())
				{
					AddTreeOwnership();
				}
				ReloadSurface();
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

			private Vector3[,] GetControlNodes()
			{
				Vector3[,] CN = new Vector3[CNSize.X, CNSize.Y];
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
				Vector3[,] STM = await Task.Run(() => GetSurfaceTransforms(NB, MB, NPB, MPB, CN, SVMSize));
				Vector3[,] SNM = await Task.Run(() => GetSurfaceNormals(NB, MB, NBD, MBD, NPB, MPB, NPBD, MPBD, CN, SVMSize));
				
				ArrayMesh ArrMesh = new ArrayMesh();

				var SurfaceArray = new Godot.Collections.Array();

				SurfaceArray.Resize((int)Mesh.ArrayType.Max);

				SurfaceArray[(int)Mesh.ArrayType.Vertex] = WindTriangles(STM);
				SurfaceArray[(int)Mesh.ArrayType.Normal] = WindTriangles(SNM);
				SurfaceArray[(int)Mesh.ArrayType.TexUV] = WindTriangles(UVs);

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

			private static Vector3[,] GetSurfaceTransforms(Matrix NB, Matrix MB, Matrix NPB, Matrix MPB, Vector3[,] CN, ByteVector2 SVMSize)
			{

				Vector3[,] STMForklift = new Vector3[SVMSize.X, SVMSize.Y];
				for (byte u = 0; u < SVMSize.X; u++)
				{
					for (byte v = 0; v < SVMSize.Y; v++)
					{
						STMForklift[u, v] = ComputeVertexVector(u, v, NB, MB, NPB, MPB, CN, SVMSize);
					}
				}
				return STMForklift;
			}
			
			private static Vector3[,] GetSurfaceNormals(Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Matrix NPB, Matrix MPB, Matrix NPBD, Matrix MPBD, Vector3[,] CN, ByteVector2 SVMSize)
			{

				Vector3[,] SNMForklift = new Vector3[SVMSize.X, SVMSize.Y];				
				
				
				for (byte u = 0; u < SVMSize.X; u++)
				{
					for (byte v = 0; v < SVMSize.Y; v++)
					{
						SNMForklift[u, v] = ComputeVertexNormal(u, v, NB, MB, NBD, MBD, NPB, MPB, NPBD, MPBD, CN, SVMSize);
					}
				}


				return SNMForklift;
			}

			private static Vector3 ComputeVertexNormal(byte u, byte v, Matrix NB, Matrix MB, Matrix NBD, Matrix MBD, Matrix NPB, Matrix MPB, Matrix NPBD, Matrix MPBD, Vector3[,] CN, ByteVector2 SVMSize)
			{
				Vector3 TangentA = ComputeVertexVector(u, v, NBD, MB, NPBD, MPB, CN, SVMSize);
				Vector3 TangentB = ComputeVertexVector(u, v, NB, MBD, NPB, MPBD, CN, SVMSize);

				if (CN[0, 0].X < 0)
				{
					TangentA.X = Abs(TangentA.X);
					TangentA.Y = Abs(TangentA.Y);
					TangentA.Z = Abs(TangentA.Z);
				} else if (CN[0, 0].Z < 0)
				{
					TangentB.X = Abs(TangentB.X);
					TangentB.Y = Abs(TangentB.Y);
					TangentB.Z = Abs(TangentB.Z);
				}

				Vector3 Normal = TangentA.Cross(TangentB).Normalized();
				
				return Normal;
			}

			private static Vector3 ComputeVertexVector(byte u, byte v, Matrix NB, Matrix MB, Matrix NPB, Matrix MPB, Vector3[,] CN, ByteVector2 SVMSize)
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
				for (int i = 0; i < CN.GetLength(0); i++)
				{
					for (int j = 0; j < CN.GetLength(1); j++)
					{
						cNX[i, j] = CN[i, j].X;
						cNY[i, j] = CN[i, j].Y;
						cNZ[i, j] = CN[i, j].Z;
					}
				}

				// Do the main math:

				Vector3 vector = new Vector3(0, 0, 0); 
				
				Matrix pUProdTMB = pU.Product(MB);
				Matrix tNBProdPV = NB.Product(pV);

				vector.X = pUProdTMB.Product(cNX).Product(tNBProdPV)[0,0];
				vector.Y = pUProdTMB.Product(cNY).Product(tNBProdPV)[0,0];
				vector.Z = pUProdTMB.Product(cNZ).Product(tNBProdPV)[0,0];
				
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

				private static int[] GenerateTriangleLoDIndexMap(Vector3[,] TriangleArray, byte LODLevel, ByteVector2 SVMSize)
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
