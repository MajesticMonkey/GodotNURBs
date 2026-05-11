using Godot;
using System;
using System.Numerics;

#nullable enable
namespace BezierSurfaces.Types
{
	namespace VectorVariants
	{
		namespace HalfVector
		{
			public struct HalfVector2 : IEquatable<HalfVector2>
			{
				public HalfVector2(Half x, Half y)
				{
					X = x;
					Y = y;
				}
				
				public Half X { get; }
				public Half Y { get; }
				
				public override string ToString() => $"({X}, {Y})";

				public bool Equals(HalfVector2 other)
				{
					return X == other.X && Y == other.Y;
				}

				public override bool Equals(object? obj)
				{
					return obj is HalfVector2 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y);
    			}

				public static bool operator ==(HalfVector2 left, HalfVector2 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(HalfVector2 left, HalfVector2 right)
				{
					return !left.Equals(right);
				}
			}
			
			public struct HalfVector3 : IEquatable<HalfVector3>
			{
				public HalfVector3(Half x, Half y, Half z)
				{
					X = x;
					Y = y;
					Z = z;
				}
				
				public Half X { get; init; }
				public Half Y { get; init; }
				public Half Z { get; init; }
				
				public override string ToString() => $"({X}, {Y}, {Z})";

				public bool Equals(HalfVector3 other)
				{
					return X == other.X && Y == other.Y && Z == other.Z;
				}

				public override bool Equals(object? obj)
				{
					return obj is HalfVector3 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y, Z);
    			}

				public static bool operator ==(HalfVector3 left, HalfVector3 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(HalfVector3 left, HalfVector3 right)
				{
					return !left.Equals(right);
				}
			}
		}

		namespace ByteVector
		{
			public struct ByteVector2 : IEquatable<ByteVector2>
			{
				public ByteVector2(byte x, byte y)
				{
					X = x;
					Y = y;
				}
				
				public byte X { get; }
				public byte Y { get; }
				
				public override string ToString() => $"({X}, {Y})";

				public bool Equals(ByteVector2 other)
				{
					return X == other.X && Y == other.Y;
				}

				public override bool Equals(object? obj)
				{
					return obj is ByteVector2 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y);
    			}

				public static bool operator ==(ByteVector2 left, ByteVector2 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(ByteVector2 left, ByteVector2 right)
				{
					return !left.Equals(right);
				}
			}
			
			public struct ByteVector3 : IEquatable<ByteVector3>
			{
				public ByteVector3(byte x, byte y, byte z)
				{
					X = x;
					Y = y;
					Z = z;
				}
				
				public byte X { get; }
				public byte Y { get; }
				public byte Z { get; }
				
				public override string ToString() => $"({X}, {Y}, {Z})";

				public bool Equals(ByteVector3 other)
				{
					return X == other.X && Y == other.Y && Z == other.Z;
				}

				public override bool Equals(object? obj)
				{
					return obj is ByteVector3 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y, Z);
    			}

				public static bool operator ==(ByteVector3 left, ByteVector3 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(ByteVector3 left, ByteVector3 right)
				{
					return !left.Equals(right);
				}
			}
		}

		namespace BitVector
		{
			public struct BitVector2
			{
				public BitVector2(bool x, bool y)
				{
					X = x;
					Y = y;
				}
				
				public bool X { get; }
				public bool Y { get; }
				
				public override string ToString() => $"({X}, {Y})";

				public bool Equals(BitVector2 other)
				{
					return X == other.X && Y == other.Y;
				}

				public override bool Equals(object? obj)
				{
					return obj is BitVector2 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y);
    			}

				public static bool operator ==(BitVector2 left, BitVector2 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(BitVector2 left, BitVector2 right)
				{
					return !left.Equals(right);
				}
			}
			
			public struct BitVector3
			{
				public BitVector3(bool x, bool y, bool z)
				{
					X = x;
					Y = y;
					Z = z;
				}
				
				public bool X { get; init; }
				public bool Y { get; init; }
				public bool Z { get; init; }
				
				public override string ToString() => $"({X}, {Y}, {Z})";

				public bool Equals(BitVector3 other)
				{
					return X == other.X && Y == other.Y && Z == other.Z;
				}

				public override bool Equals(object? obj)
				{
					return obj is BitVector3 other && Equals(other);
				}

				public override int GetHashCode()
    			{
					return HashCode.Combine(X, Y, Z);
    			}

				public static bool operator ==(BitVector3 left, BitVector3 right)
				{
					return left.Equals(right);
				}

				public static bool operator !=(BitVector3 left, BitVector3 right)
				{
					return !left.Equals(right);
				}
			}
		}
	}
	namespace Matrix
	{
		public struct Matrix
		{
			
			public float[,] M;
			
			// Constructor
			public Matrix(int Rows, int Columns)
			{
				M = new float[Rows, Columns];
			}
			
			public Matrix Product(Matrix that)
			{
				this.Compatible(that);
				Matrix product = new Matrix(this.M.GetLength(0), that.M.GetLength(1));
				for (int i = 0; i < product.GetLength(0); i++)
				{
					for (int j = 0; j < product.GetLength(1); j++)
					{
						product.M[i, j] = this.DotProduct(that, i, j);
					}
				}
				return product;
			}
			/*
			public Matrix GPUProduct(Matrix that)
			{
				this.Compatible(that);

				var rd = RenderingServer.CreateLocalRenderingDevice();
				var shaderFile = GD.Load<RDShaderFile>("res://addons/beziersurfaces/Scripts/MultiplyMatricies.glsl");
				var shaderBytecode = shaderFile.GetSpirV();
				var shader = rd.ShaderCreateFromSpirV(shaderBytecode);

				uint[] dimensions = [(uint)this.M.GetLength(0), (uint)this.M.GetLength(1), (uint)that.M.GetLength(0), (uint)that.M.GetLength(1), (uint)this.M.GetLength(0), (uint)that.M.GetLength(1)];

				float[] flatThis = Flatten(this.M);
				float[] flatThat = Flatten(that.M);
				float[] flatProduct = new float[this.M.GetLength(0) * that.M.GetLength(1)];

				// Create the byte arrays for the buffers
				byte[] inputBytesDimensions = new byte[dimensions.Length * sizeof(float)];
				byte[] inputBytesThis = new byte[flatThis.Length * sizeof(float)];
				byte[] inputBytesThat = new byte[flatThat.Length * sizeof(float)];
				byte[] inputBytesProduct = new byte[flatProduct.Length * sizeof(float)];
				
				// Copy the data into the byte arrays
				Buffer.BlockCopy(dimensions, 0, inputBytesDimensions, 0, inputBytesDimensions.Length);
				Buffer.BlockCopy(flatThis, 0, inputBytesThis, 0, inputBytesThis.Length);
				Buffer.BlockCopy(flatThat, 0, inputBytesThat, 0, inputBytesThat.Length);
				Buffer.BlockCopy(flatProduct, 0, inputBytesProduct, 0, inputBytesProduct.Length);
				
				// Create the buffers
				var bufferDims = rd.StorageBufferCreate((uint)inputBytesDimensions.Length, inputBytesDimensions);
				var bufferThis = rd.StorageBufferCreate((uint)inputBytesThis.Length, inputBytesThis);
				var bufferThat = rd.StorageBufferCreate((uint)inputBytesThat.Length, inputBytesThat);
				var bufferProduct = rd.StorageBufferCreate((uint)inputBytesProduct.Length, inputBytesProduct);	

				// Create Uniforms
				var uDims = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 0 };
				var uThis = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 1 };
				var uThat = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 2 };
				var uProduct = new RDUniform { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 3 };

				// Bind the buffers to the shader
				uDims.AddId(bufferDims);
				uThis.AddId(bufferThis);
				uThat.AddId(bufferThat);
				uProduct.AddId(bufferProduct);
				var uniformSet = rd.UniformSetCreate([uDims, uThis, uThat, uProduct], shader, 0);

				// Create the compute pipeline
				var pipeline = rd.ComputePipelineCreate(shader);
				var computeList = rd.ComputeListBegin();
				rd.ComputeListBindComputePipeline(computeList, pipeline);
				rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
				rd.ComputeListDispatch(computeList, xGroups: dimensions[4], yGroups: dimensions[5], zGroups: dimensions[5]);
				rd.ComputeListEnd();
				rd.Submit();
				rd.Sync();

				var outputBytes = rd.BufferGetData(bufferProduct);
				var flatOutput = new float[dimensions[4] * dimensions[5]];
				Buffer.BlockCopy(outputBytes, 0, flatOutput, 0, outputBytes.Length);

				rd.FreeRid(shader);

				return Expand(flatOutput, dimensions[4], dimensions[5]);				
			}
			*/
			public float DotProduct(Matrix that, int n, int m)
			{
				float dot = 0;
				for (int i = 0; i < this.M.GetLength(1); i++) 
				{
					dot += this.M[n, i] * that.M[i, m];
				}
				return dot;
			}
			
			public Matrix Transpose()
			{
				Matrix forklift = new Matrix(this.M.GetLength(1), this.M.GetLength(0));
				for (int i = 0; i < this.M.GetLength(1); i++)
				{
					for (int j = 0; j < this.M.GetLength(0); j++)
					{
						forklift.M[i, j] = this.M[j, i];
					}
				}
				return forklift;
			}
			
			private void Compatible(Matrix that)
			{
				if (this.M.GetLength(1) != that.M.GetLength(0))
				{
					throw new IncompatibleMatricies($"The provided matricies ({this.M.GetLength(0)}x{this.M.GetLength(1)}, {that.M.GetLength(0)}x{that.M.GetLength(1)}) are incompatible for multiplication.");
				}
			}
			
			public float this[int x, int y]
			{
				get => M[x, y];
				set => M[x, y] = value;
			}

			public void Print()
			{
				GD.Print("Printing Matrix");
				for (int i = 0; i < this.M.GetLength(0); i++)
				{
					string printer = "";
					for (int j = 0; j < this.M.GetLength(1); j++)
					{
						printer += this.M[i, j].ToString() + ", ";
					}
					GD.Print(printer);
				}
				
			}

			public int GetLength(int dim) => M.GetLength(dim);
			
			[Serializable]
			private class IncompatibleMatricies : Exception
			{
				public IncompatibleMatricies() { }
				
				public IncompatibleMatricies(string message) : base(message) { }
				
				public IncompatibleMatricies(string message, Exception inner) : base(message, inner) { }
			}
		}
	}
}
