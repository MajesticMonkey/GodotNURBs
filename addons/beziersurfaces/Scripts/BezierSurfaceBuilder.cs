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
	[GlobalClass, Tool]
	public partial class BezierSurfaceBuilder : Node3D
	{
		Action<string> Print = (a) => GD.Print(a);
		
		private List<BezierSurface> SurfaceNetwork = new List<BezierSurface>();
		public List<List<ControlPoint>> ControlNetwork = new List<List<ControlPoint>>();
		public List<List<Vector4>> ControlNetworkPositions = new List<List<Vector4>>();
		
		[Export]
		public bool AutoUpdate = true; // Whether or not to continuously update the surfaces.
		[Export]
		public bool EdgeDebugMode = false;


		[ExportGroup("Control Nodes")]
		
		[Export]
		public Mesh CNShape = new SphereMesh();
		
		[ExportSubgroup("Network Size")]
		
		[Export]
		public byte CNXSize = 4;
		[Export]
		public byte CNYSize = 4;
		
		public ByteVector2 CNSize = new ByteVector2(0, 0); // Control Net Size (Number of control points per surface)
		
		[ExportGroup("Vertex Map Size")]
		[Export]
		public byte SVMXSize = 32;
		[Export]
		public byte SVMYSize = 32;
		
		public ByteVector2 SVMSize = new ByteVector2(0, 0); // Surface Vertex Map Size (Vertex Density)
		
		[ExportGroup("Surface Size")]
		[Export]
		public byte SXSize = 32; // Surface X Size
		[Export]
		public byte SYSize = 32; // Surface X Size

		public ByteVector2 SSize = new ByteVector2(0, 0); // Surface Size (Nummber of game units a surface extends over, only applies to creation of control points.)
		
		[ExportGroup("Material")]
		[Export]
		public Material SurfaceMaterial;

		//[ExportGroup("Level of Detail")]
		//[Export]
		public Camera3D LODCamera; // Camera used for LOD, currently defunct.
		//[Export]
		public Godot.Collections.Array<Vector2> LODDistances; // X is the distance it activates, Y is the number of verticies it skips when that happens. Currently defunct.
		

		public Matrix NB;
		public Matrix MB;
		public Matrix NBD;
		public Matrix MBD;
		public Matrix NPB;
		public Matrix MPB;

		public Vector2 ControlPointSpacing;

		public readonly String BezierPrefix = "BezierSurface_";
		public readonly String NodePrefix = "ControlPoint_";
		public readonly String NodeHasSurfaceMetaName = "HasSurface";

		private Godot.ProgressBar ProgressBar = new Godot.ProgressBar();

		private bool Loaded = false;

		public BezierSurfaceBuilder()
		{
			UpdateMaintenance();
		}

		public override void _Ready()
		{
			Print("Ready!");
			if (!Loaded)
			{
				Print("Loading!");
				Loaded = true;
				LoadSurfaces();
			}

			if (!Engine.IsEditorHint())
			{
				for (int i = 0; i < ControlNetwork.Count; i++)
				{
					for (int j = 0; j < ControlNetwork[i].Count; j++)
					{
						ControlNetworkPositions[i][j] = ControlNetwork[i][j].GetPosVec();
						ControlNetwork[i][j].QueueFree();
					}
				}
			}
			UpdateAllSurfaces();
		}

		public override void _Process(double delta)
		{
			if (AutoUpdate && Engine.IsEditorHint())
			{
				UpdateSurfaces();
			}
		}

		public void LoadSurfaces()
		{
			SurfaceNetwork = new List<BezierSurface>();
			ControlNetwork = new List<List<ControlPoint>>();
			ControlNetworkPositions = new List<List<Vector4>>();

			ReattainChildren();
			
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					if (((string)ControlNetwork[i][j].Name)[^1] == '_')
					{
						CreateSurface(new Vector2(i, j));
					}
				}
			}
		}

		public void ReattainChildren()
		{
			Godot.Collections.Array<Godot.Node> children = GetChildren();

			if(children.Count == 0)
			{
				ControlNetwork.Add(new List<ControlPoint>());
				ControlNetworkPositions.Add(new List<Vector4>());
				ControlNetwork[0].Add(ConstControlPoint(new Vector3(0, 0, 0)));
				ControlNetwork[0][0].Position = new Vector3(0, 0, 0);
				ControlNetwork[0][0].Weight = 1.0f;
				ControlNetworkPositions[0].Add(ControlNetwork[0][0].GetPosVec());
				return;
			}

			while (children.Count > 0)
			{
				for (int i = 0; i < children.Count; i++)
				{
					if (children[i] is ControlPoint Point)
					{
						if (Point.Loc.X < ControlNetwork.Count)
						{
							if ((float)ControlNetwork[(int)Point.Loc.X].Count == Point.Loc.Y)
							{
								ControlNetwork[(int)Point.Loc.X].Add(Point);
								ControlNetworkPositions[(int)Point.Loc.X].Add(Vector4.Zero);
								RemoveChild(Point);
								AddControlPoint(Point);
								children.RemoveAt(i);
							}
						}
						else if (Point.Loc.X == ControlNetwork.Count)
						{
							ControlNetwork.Add(new List<ControlPoint>());
							ControlNetworkPositions.Add(new List<Vector4>());
						}
					} else {
						children[i].QueueFree();
						children.RemoveAt(i);
					}
				}
			}
		}

		public void UpdateMaintenance()
		{
			bool RecalcNB = !(CNSize.X == CNXSize);
			bool RecalcMB = !(CNSize.Y == CNYSize);

			ByteVector2 NewCNSize = new ByteVector2(CNXSize, CNYSize);
			ByteVector2 NewSVMSize = new ByteVector2(SVMXSize, SVMYSize);
			ByteVector2 NewSSize = new ByteVector2(SXSize, SYSize);

			if (NewCNSize != CNSize)
			{

			}
			if (NewSVMSize != SVMSize)
			{

			}
			if (NewSSize != SSize)
			{

			}

			CNSize = NewCNSize;
			SVMSize = NewSVMSize;
			SSize = NewSSize;

			if (RecalcNB)
			{
				NB = BernsteinPolynomial(CNSize.X);
				NBD = DifferentiateBernstein(CNSize.X);
				NPB = PowerBasis(CNSize.X);
			}
			if (RecalcMB)
			{
				MB = BernsteinPolynomial(CNSize.Y).Transpose();
				MBD = DifferentiateBernstein(CNSize.Y).Transpose();
				MPB = PowerBasis(CNSize.Y);
			}

			ControlPointSpacing = new Vector2((float)SSize.X/((float)CNSize.X - (float)1), (float)SSize.Y/((float)CNSize.Y - (float)1));
		}

		

		public async void UpdateAllSurfaces()
		{
			UpdateMaintenance();
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				SurfaceNetwork[i].ReloadSurface();
			}
		}

		public async void UpdateSurfaces()
		{
			UpdateMaintenance();
			List<ControlPoint> outdatedControlNodes = new List<ControlPoint>();
			for (int i = 0; i < ControlNetwork.Count; i++)
			{
				for (int j = 0; j < ControlNetwork[i].Count; j++)
				{
					if (ControlNetwork[i][j].GetPosVec() != ControlNetworkPositions[i][j])
					{
						outdatedControlNodes.Add(ControlNetwork[i][j]);
						ControlNetworkPositions[i][j] = ControlNetwork[i][j].GetPosVec();
					}
				}
			}

			List<BezierSurface> outdatedSurfaces = new List<BezierSurface>();
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				for (int j = 0; j < outdatedControlNodes.Count; j++)
				{
					if (SurfaceNetwork[i].IsMyControlNode(outdatedControlNodes[j].Loc))
					{
						outdatedSurfaces.Add(SurfaceNetwork[i]);
					}
				}
			}

			for (int i = 0; i < outdatedSurfaces.Count; i++)
			{
				outdatedSurfaces[i].ReloadSurface();
			}
		}
		
		public void CreateSurfaceExternally(Vector2 Loc)
		{
			CreateSurface(Loc);
		}

		private BezierSurface CreateSurface(Vector2 Loc)
		{
			ControlNetwork[(int)Loc.X][(int)Loc.Y].HasSurface = true;
			for (int i = 0; i < Loc.X + CNSize.X; i++)
			{
				if (i == ControlNetwork.Count && i != Loc.X + CNSize.X)
				{
					ControlNetwork.Add(new List<ControlPoint>());
					ControlNetworkPositions.Add(new List<Vector4>());
				}
				for (int j = 0; j < Loc.Y + CNSize.Y; j++)
				{
					if (j == ControlNetwork[i].Count && j != Loc.Y + CNSize.Y)
					{
						AddPoint(i, j);
					}
				}
			}

			BezierSurface surface = new BezierSurface(this, Loc);

			SurfaceNetwork.Add(surface);
			
			return surface;
		}

		private void AddPoint(int i, int j, float w = 1.0f)
		{
			ControlNetwork[i].Add(ConstControlPoint(new Vector3(i, j, w)));
			ControlNetwork[i][j].Position = new Vector3((float)i*ControlPointSpacing.X, 0, (float)j*ControlPointSpacing.Y);
			ControlNetwork[i][j].Weight = w;
			ControlNetworkPositions[i].Add(ControlNetwork[i][j].GetPosVec());
		}

		public void RemoveSurfaceExternally(Vector2 Loc)
		{
			RemoveSurface(Loc);
		}

		private void RemoveSurface(Vector2 Loc)
		{
			for (int i = 0; i < SurfaceNetwork.Count; i++)
			{
				if (SurfaceNetwork[i].CNLoc == Loc)
				{
					SurfaceNetwork[i].SetOwner(null);
					SurfaceNetwork[i].QueueFree();
					SurfaceNetwork.RemoveAt(i);
				}
			}
		}
		
		public ControlPoint ConstControlPoint(Vector3 Loc) // Construct Control Point
		{
			ControlPoint meshInstance = new ControlPoint();
			
			meshInstance.Mesh = CNShape;

			AddControlPoint(meshInstance);

			meshInstance.SetNameWithUVW(Loc);
			return meshInstance;
		}

		public void AddControlPoint(ControlPoint Point)
		{
			AddChild(Point, true, Node.InternalMode.Front);
			var theTree = GetTree().GetEditedSceneRoot();
			Point.SetOwner(theTree);
		}

		#region Bernsteins
			public static Matrix BernsteinPolynomial(int n)
			{
				Matrix B = new Matrix(n, n);
				n--;
				for (int i = 0; i <= n; i++)
				{
					for (int j = 0; j <= n; j++)
					{
						if (j >= i)
						{
							B[i, j] = (int)(BinomialCoefficient(n, i)*BinomialCoefficient(n-i, j-i)*(float)(Math.Pow(-1, j-i)));
						}
						else
						{
							B[i, j] = 0;
						}
					}
				}
				return B;
			}

			public static Matrix PowerBasis(int n)
			{ // Creates an array with a number of indexes "n", where each value equals its index
				Matrix a = new Matrix(n, 1);
				for (int i = 0; i < n; i++)
				{
					a[i, 0] = i;
				}
				return a;
			}

			static private float BinomialCoefficient(int n, int k)
			{
				int a = Factorial(n);
				int b = Factorial(k)*Factorial(n-k);
				return a/b;
			}

			private Matrix DifferentiateBernstein(int n)
			{
				Matrix B = BernsteinPolynomial(n);
				Matrix DB = new Matrix(n, n);
				Matrix Pows = PowerBasis(n);
				int nreduc = n - 1;
				for (int i = 0; i < n; i++)
				{
					for (int j = 0; j < nreduc; j++)
					{
						DB[i, j] = B[i, j + 1] * Pows[j + 1, 0];
					}
				}
				return DB;
			}

			static private int Factorial(int n)
			{
				if (n == 0) { return 1; }

				int k = n;
				for (var i = n - 1; i > 0; i--)
				{
					k *= i;
				}
				return k;
			}
		#endregion
	}
}
