using Godot;
using System;
using System.Text;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct GravitatingParticle(float mass, Vector2 position, Vector2 velocity, Vector4 color) {
	public float mass = mass;
	public Vector2 position = position;
	public Vector2 velocity = velocity;
	public Vector2 acceleration = new(0.0f, 0.0f);
	public Vector4 color = color;
};

public struct ComputeShader {
	public Rid shader;
	public Rid pipeline;
	public Rid uniformSetA;
	public Rid uniformSetB;

	private readonly bool use_two_sets;
	private readonly RenderingDevice rd;

	public ComputeShader(string shaderPath, TransformBuffer[] transforms, RenderingDevice rd, Rid[] buffers = null, Rid[] textures = null, bool use_two_sets = true) {
		this.rd = rd;
		this.use_two_sets = use_two_sets;

		shader = LoadShaderFromFile(shaderPath);
		pipeline = rd.ComputePipelineCreate(shader);

		int bufferCount = 0;

		if (buffers != null) {
			bufferCount = buffers.Length;
		}

		int textureCount = 0;

		if (textures != null) {
			textureCount = textures.Length;
		}

		if (use_two_sets) {
			RDUniform[] uniformsA = new RDUniform[transforms.Length + bufferCount + textureCount];
			RDUniform[] uniformsB = new RDUniform[transforms.Length + bufferCount + textureCount];

			for (int i = 0; i < transforms.Length; i++) {
				RDUniform uniformA = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = i };
				uniformA.AddId(transforms[i].bufferA);
				uniformsA[i] = uniformA;

				RDUniform uniformB = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = i };
				uniformB.AddId(transforms[i].bufferB);
				uniformsB[i] = uniformB;
			}

			for (int i = 0; i < bufferCount; i++) {
				RDUniform uniform = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = transforms.Length + i };
				uniform.AddId(buffers[i]);
				uniformsA[transforms.Length + i] = uniform;
				uniformsB[transforms.Length + i] = uniform;
			}

			for (int i = 0; i < textureCount; i++) {
				RDUniform uniform = new() { UniformType = RenderingDevice.UniformType.Image, Binding = transforms.Length + bufferCount + i };
				uniform.AddId(textures[i]);
				uniformsA[transforms.Length + bufferCount + i] = uniform;
				uniformsB[transforms.Length + bufferCount + i] = uniform;
			}

			uniformSetA = rd.UniformSetCreate(
				[.. uniformsA],
				shader, 0
			);

			uniformSetB = rd.UniformSetCreate(
				[.. uniformsB],
				shader, 0
			);
		}
		else {
			RDUniform[] uniforms = new RDUniform[transforms.Length*2 + bufferCount + textureCount];

			for (int i = 0; i < transforms.Length; i++) {
				RDUniform uniformA = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 2*i };
				RDUniform uniformB = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = 2*i + 1 };
				uniformA.AddId(transforms[i].bufferA);
				uniformB.AddId(transforms[i].bufferB);

				uniforms[2*i] = uniformA;
				uniforms[2*i + 1] = uniformB;
			}

			for (int i = 0; i < bufferCount; i++) {
				RDUniform uniform = new() { UniformType = RenderingDevice.UniformType.StorageBuffer, Binding = transforms.Length*2 + i };
				uniform.AddId(buffers[i]);
				uniforms[transforms.Length*2 + i] = uniform;
			}

			for (int i = 0; i < textureCount; i++) {
				RDUniform uniform = new() { UniformType = RenderingDevice.UniformType.Image, Binding = transforms.Length*2 + bufferCount + i };
				uniform.AddId(textures[i]);
				uniforms[transforms.Length*2 + bufferCount + i] = uniform;
			}

			uniformSetA = rd.UniformSetCreate(
				[.. uniforms],
				shader, 0
			);
		}

		Validate();
	}

	public readonly void Bind(long computeList, object[] pushConstants, uint groups, bool useBufferB) {
		byte[] pushConstantsBytes = ConstantsToBytes(pushConstants);

		rd.ComputeListBindComputePipeline(computeList, pipeline);

		if (useBufferB & use_two_sets) {
			rd.ComputeListBindUniformSet(computeList, uniformSetB, 0);
		}
		else {
			rd.ComputeListBindUniformSet(computeList, uniformSetA, 0);
		}

		rd.ComputeListSetPushConstant(computeList, pushConstantsBytes, (uint)pushConstantsBytes.Length);
		rd.ComputeListDispatch(computeList, groups, 1, 1);
	}

	private readonly Rid LoadShaderFromFile(string filePath) {
		RDShaderFile shaderFile = GD.Load<RDShaderFile>(filePath);
		RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();

		return rd.ShaderCreateFromSpirV(shaderBytecode);
	}

	public readonly void Free() {
		rd.FreeRid(uniformSetA);

		if (uniformSetB.IsValid) {
			rd.FreeRid(uniformSetB);
		}
		
		rd.FreeRid(pipeline);
		rd.FreeRid(shader);
	}

	public readonly void Validate() {
		if (!shader.IsValid) {
			throw new Exception("Shader RID is not valid.");
		}
		if (!pipeline.IsValid) {
			throw new Exception("Pipeline RID is not valid.");
		}
		if (!uniformSetA.IsValid) {
			throw new Exception("Uniform set A RID is not valid.");
		}
		if (!uniformSetB.IsValid & use_two_sets) {
			throw new Exception("Uniform set B RID is not valid");
		}

		GD.Print("Shader, pipeline, and uniform sets are valid.");
	}

	private static byte[] ConstantsToBytes(params object[] constants) {
		byte[] bytes = new byte[constants.Length * 4];

		for (int i = 0; i < constants.Length; i++) {
			byte[] fieldBytes = constants[i] switch {
				float f => BitConverter.GetBytes(f),
				uint u => BitConverter.GetBytes(u),
				int n => BitConverter.GetBytes(n),
				_ => throw new ArgumentException($"Unsupported push constant type: {constants[i].GetType()} at index {i}")
			};

			fieldBytes.CopyTo(bytes, i * 4);
		}

		return bytes;
	}
}

public class TransformBuffer(uint size, RenderingDevice rd) {
	public readonly Rid bufferA = rd.StorageBufferCreate(size);
	public readonly Rid bufferB = rd.StorageBufferCreate(size);

	private readonly RenderingDevice rd = rd;

	public void Update(byte[] bytes, bool useBufferB) {
		if (!useBufferB) {
			rd.BufferUpdate(bufferA, 0, (uint)bytes.Length, bytes);
		}
		else {
			rd.BufferUpdate(bufferB, 0, (uint)bytes.Length, bytes);
		}
	}

	public void Free() {
		rd.FreeRid(bufferA);
		rd.FreeRid(bufferB);
	}
}

public partial class NBodySimulationGPU : Node2D {

	private RenderingDevice rd;

	private ComputeShader workgroupBoundsShader, globalBoundsShader, mortonEncodeShader;
	private ComputeShader radixHistogramShader, radixWorkgroupScanShader, radixGlobalScanShader, radixIndexScatterShader, radixFullScatterShader;
	private ComputeShader radixTrieShader;
	private ComputeShader naiveForcesShader;
	private ComputeShader integrationShader;

	private bool useBufferB = false;
	private readonly TransformBuffer[] transformBuffers = new TransformBuffer[7];

	private enum TBN { Position, Velocity, Acceleration, Mass, Color, Morton, RadixIndex};

	private Rid boundsBuffer, globalHistogramBuffer, threadHistogramBuffer, scanBuffer;
	private Rid quadnodeParentBuffer, quadnodeChildrenBuffer, quadnodePositionBuffer, quadnodeSizeBuffer, quadnodeMassBuffer, quadnodeCOMBuffer;
	private Rid particleTransformTexture, particleColorTexture;
	private uint textureWidth, textureHeight;

	private MultiMeshInstance2D multiMeshInstance;
	private MultiMesh multiMesh;
	private ShaderMaterial mat;
	public Vector2 cameraOffset;
	
	private Timer debugTimer;

	private bool initializing;

	[ExportGroup("Debug Settings")]
	[Export] public bool debugMode = false;
	[Export] public bool debugBothBuffers = false;

	[Export(PropertyHint.Flags, "Position, Velocity, Acceleration, Mass, Color, Morton, RadixIndex, CheckMortons")]
	public uint DebugParticleProperties {get; set;} = 0;

	[Export] public bool debugBounds = false;
	[Export] public bool debugRadixSort = false;
	[Export] public bool debugZOrderCurve;
	[Export] public float debugInterval = 1.0f;
	public float gravitationalConstant = 10_000.0f;

	[ExportGroup("Generation Settings")]

	[Export(PropertyHint.Enum, "Circle, Square")] 
	public uint positionGenerationType = 0;

	[Export] public float particleSize = 10.0f;
	public float epsilon = 0.1f;

	[Export(PropertyHint.Enum, "Naive, BarnesHut, FMM")] public uint algorithm = 0;
	public float timeScale = 1.0f;

	public int nextParticleCount = 1;
	private int particleCount = 1;

	[Export(PropertyHint.Enum, "Zero, Random, Negative Position Perp., Position Perp.")] public uint velocityGenerationType = 0;

	[ExportGroup("Compute Shader Settings")]
	[Export] public float workGroupSize = 256.0f;
	[Export] public uint radixBits = 8;
	[Export] public uint radixKeysPerThread = 8;
	private uint radixBase;
	[Export] public uint mortonBitsPerAxis = 16;

	private Vector2[] zOrderLinePositions;
	private Color[] zOrderLineColors;

	public GravitatingParticle[] particles;

	private void InitializeShaders() {
		transformBuffers[(int)TBN.Position] = new TransformBuffer(2 * sizeof(float) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.Velocity] = new TransformBuffer(2 * sizeof(float) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.Acceleration] = new TransformBuffer(2 * sizeof(float) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.Mass] = new TransformBuffer(sizeof(float) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.Color] = new TransformBuffer(4 * sizeof(float) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.Morton] = new TransformBuffer(sizeof(uint) * (uint)particleCount, rd);
		transformBuffers[(int)TBN.RadixIndex] = new TransformBuffer(sizeof(uint) * (uint)particleCount, rd);

		boundsBuffer = rd.StorageBufferCreate(sizeof(float) * 4 * (uint)Mathf.CeilToInt(particleCount / workGroupSize));

		uint histogramGroups = (uint)Mathf.CeilToInt(particleCount / (workGroupSize*radixKeysPerThread));
		globalHistogramBuffer = rd.StorageBufferCreate(sizeof(uint) * radixBase * histogramGroups);
		threadHistogramBuffer = rd.StorageBufferCreate(sizeof(uint) * radixBase * (uint)workGroupSize * histogramGroups);
		scanBuffer = rd.StorageBufferCreate((uint)(sizeof(uint) * radixBase * (Mathf.CeilToInt(particleCount / workGroupSize)+1)));

		particleTransformTexture = CreateParticleTexture(particleCount);
		particleColorTexture = CreateParticleTexture(particleCount);

		naiveForcesShader = new ComputeShader("res://Shaders/Naive/forces.glsl", [transformBuffers[(int)TBN.Position], transformBuffers[(int)TBN.Velocity], transformBuffers[(int)TBN.Acceleration], transformBuffers[(int)TBN.Mass]], rd);

		workgroupBoundsShader = new ComputeShader("res://Shaders/Morton/workgroup_bounds.glsl", [transformBuffers[(int)TBN.Position]], rd, [boundsBuffer]);
		globalBoundsShader = new ComputeShader("res://Shaders/Morton/global_bounds.glsl", [], rd, [boundsBuffer]);
		mortonEncodeShader = new ComputeShader("res://Shaders/Morton/encode_positions.glsl", [transformBuffers[(int)TBN.Position], transformBuffers[(int)TBN.Morton]], rd, [boundsBuffer]);
		
		radixHistogramShader = new ComputeShader("res://Shaders/Radix/histogram.glsl", [transformBuffers[(int)TBN.Morton], transformBuffers[(int)TBN.RadixIndex]], rd, [globalHistogramBuffer], null, false);
		radixWorkgroupScanShader = new ComputeShader("res://Shaders/Radix/workgroup_scan.glsl", [], rd, [globalHistogramBuffer, scanBuffer]);
		radixGlobalScanShader = new ComputeShader("res://Shaders/Radix/global_scan.glsl", [], rd, [globalHistogramBuffer, scanBuffer]);
		radixIndexScatterShader = new ComputeShader("res://Shaders/Radix/index_scatter.glsl", [transformBuffers[(int)TBN.Morton], transformBuffers[(int)TBN.RadixIndex]], rd, [scanBuffer, threadHistogramBuffer], null, false);
		radixFullScatterShader = new ComputeShader("res://Shaders/Radix/full_scatter.glsl", [transformBuffers[(int)TBN.Position], transformBuffers[(int)TBN.Velocity], transformBuffers[(int)TBN.Acceleration], transformBuffers[(int)TBN.Mass], transformBuffers[(int)TBN.Color], transformBuffers[(int)TBN.Morton], transformBuffers[(int)TBN.RadixIndex]], rd, [], null, false);
		
		radixTrieShader = new ComputeShader("res://Shaders/Quadtree/radix_trie.glsl", [transformBuffers[(int)TBN.Morton]], rd, [quadnodeParentBuffer, quadnodeChildrenBuffer]);

		integrationShader = new ComputeShader("res://Shaders/integration.glsl", [transformBuffers[(int)TBN.Position], transformBuffers[(int)TBN.Velocity], transformBuffers[(int)TBN.Acceleration], transformBuffers[(int)TBN.Color]], rd, null, [particleTransformTexture, particleColorTexture]);

		GD.Print("Shaders initialized.");
	}

	private void InitializeMultimesh() {
		var quad = new QuadMesh {
			Size = new Vector2(particleSize, particleSize)
		};

		Shader bodiesShader = GD.Load<Shader>("res://Shaders/bodies.gdshader");
		mat = new ShaderMaterial {
			Shader = bodiesShader
		};
		mat.SetShaderParameter("particle_transform", new Texture2Drd { TextureRdRid = particleTransformTexture });
		mat.SetShaderParameter("particle_color", new Texture2Drd { TextureRdRid = particleColorTexture });
		mat.SetShaderParameter("texture_width", textureWidth);
		cameraOffset = Vector2.Zero;
		mat.SetShaderParameter("camera_offset", cameraOffset);

		multiMesh = new MultiMesh {
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			UseColors = false,
			UseCustomData = false,
			InstanceCount = particles.Length,
			VisibleInstanceCount = particles.Length,
			Mesh = quad
		};

		multiMeshInstance = new MultiMeshInstance2D {
			Multimesh = multiMesh,
			Material = mat
		};

		for (int i = 0; i < particles.Length; i++) {
			multiMesh.SetInstanceTransform2D(i, Transform2D.Identity);
		}

		AddChild(multiMeshInstance);

		GD.Print("Multimesh initialized.");
	}

	private Rid CreateParticleTexture(int particleCount) {
		textureWidth = 4096;
		textureHeight = (uint)Mathf.CeilToInt((float)particleCount / textureWidth);

		RDTextureFormat format = new() {
			Width = textureWidth,
			Height = textureHeight,
			Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat,
			UsageBits = RenderingDevice.TextureUsageBits.StorageBit
				  | RenderingDevice.TextureUsageBits.SamplingBit
				  | RenderingDevice.TextureUsageBits.CanUpdateBit
				  | RenderingDevice.TextureUsageBits.CanCopyFromBit
		};

		RDTextureView view = new();

		return rd.TextureCreate(format, view, []);
	}

	private void GenerateNewParticles() {
		particleCount = nextParticleCount;
		particles = new GravitatingParticle[particleCount];

		Random random = new();

		for (int i = 0; i < particleCount; i++) {
			float mass = (float)random.NextDouble() * 10.0f + 1.0f;
			Vector2 position = Vector2.Zero;
			
			switch (positionGenerationType) {
				case 0: // Circle
					double angle = 2.0 * Math.PI * random.NextDouble();
					float radius = (float)Math.Sqrt(random.NextDouble()) * 1000.0f;
					position = new((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
					break;
				case 1: // Square
					position = new((float)random.NextDouble() * 1000.0f - 500.0f, (float)random.NextDouble() * 1000.0f - 500.0f);
					break;
			}

			Vector2 velocity = Vector2.Zero;

			switch (velocityGenerationType) {
				case 0:
					break;
				case 1:
					velocity = new Vector2((float)random.NextDouble() * 100.0f - 50.0f, (float)random.NextDouble() * 100.0f - 50.0f);
					break;
				case 2:
					velocity = position.Rotated((float)Math.PI/-2.0f) * (float)random.NextDouble() * 0.5f;
					break;
				case 3:
					velocity = position.Rotated((float)Math.PI/2.0f) * (float)random.NextDouble() * 0.5f;
					break;
			}

			Vector4 color = new(0.4f, 0.6f, 0.7f, 1.0f);

			particles[i] = new GravitatingParticle(mass, position, velocity, color);
		}

		GD.Print("Generated ", particleCount, " new particles.");
	}

	private async void CreateNewSimulation() {
		initializing = true;
		multiMeshInstance.Visible = false;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		GenerateNewParticles();
		FreeResources();
		InitializeShaders();

		useBufferB = false;

		byte[][] bufferBytes = ParticlesToBytes();
		
		for (int i = 0; i < transformBuffers.Length; i++) {
			transformBuffers[i].Update(bufferBytes[i], useBufferB);
		}

		mat.SetShaderParameter("particle_transform", new Texture2Drd { TextureRdRid = particleTransformTexture });
		mat.SetShaderParameter("particle_color", new Texture2Drd { TextureRdRid = particleColorTexture });
		cameraOffset = Vector2.Zero;
		mat.SetShaderParameter("camera_offset", cameraOffset);

		multiMesh.InstanceCount = particles.Length;
		multiMesh.VisibleInstanceCount = particles.Length;
		for (int i = 0; i < particles.Length; i++) {
			multiMesh.SetInstanceTransform2D(i, Transform2D.Identity);
		}

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		multiMeshInstance.Visible = true;
		GD.Print("Created new simulation with ", particleCount, " particles.");
	}

	public void Initialize() {
		radixBase = (uint)1 << (int)radixBits;

		rd = RenderingServer.GetRenderingDevice();

		GenerateNewParticles();

		InitializeShaders();
		InitializeMultimesh();

		byte[][] bufferBytes = ParticlesToBytes();
		
		for (int i = 0; i < transformBuffers.Length; i++) {
			transformBuffers[i].Update(bufferBytes[i], useBufferB);
		}

		if (debugMode) {
			GD.Print("Debug mode is on");

			debugTimer = new Timer {
				WaitTime = debugInterval,
				OneShot = true
			};

			AddChild(debugTimer);


			debugTimer.Start();
		}

		GD.Print("Simulation initialized with ", particleCount, " particles.");
	}

	public override void _Process(double delta) {
		uint particleGroups = (uint)Mathf.CeilToInt(particles.Length / workGroupSize);

		long computeList = rd.ComputeListBegin();

		workgroupBoundsShader.Bind(computeList, [particleCount], particleGroups, useBufferB);
		rd.ComputeListAddBarrier(computeList);
		globalBoundsShader.Bind(computeList, [particleGroups], particleGroups, useBufferB);
		rd.ComputeListAddBarrier(computeList);

		mortonEncodeShader.Bind(computeList, [mortonBitsPerAxis, particleCount], particleGroups, useBufferB);
		rd.ComputeListAddBarrier(computeList);

		uint histogramGroups = (uint)Mathf.CeilToInt(particleCount / (workGroupSize*radixKeysPerThread));
		bool propertiesStoredInB = useBufferB;
		bool indiciesStoredInB = useBufferB;

		for (uint pass = 0; pass < 8*sizeof(uint) / radixBits; pass++) {
			radixHistogramShader.Bind(computeList, [radixBase, radixKeysPerThread, particleCount, pass*radixBits, indiciesStoredInB ? 1 : 0, propertiesStoredInB ? 1 : 0], histogramGroups, useBufferB);
			rd.ComputeListAddBarrier(computeList);
			
			radixWorkgroupScanShader.Bind(computeList, [radixBase], histogramGroups, useBufferB);
			rd.ComputeListAddBarrier(computeList);
			
			radixGlobalScanShader.Bind(computeList, [radixBase, histogramGroups], 1, useBufferB);
			rd.ComputeListAddBarrier(computeList);
		
			radixIndexScatterShader.Bind(computeList, [radixBase, particleCount, radixKeysPerThread, pass*radixBits, histogramGroups, indiciesStoredInB ? 1 : 0, propertiesStoredInB ? 1 : 0], histogramGroups, useBufferB);
			rd.ComputeListAddBarrier(computeList);

			indiciesStoredInB = !indiciesStoredInB;
		}

		radixFullScatterShader.Bind(computeList, [particleCount, radixKeysPerThread, propertiesStoredInB ? 1 : 0, indiciesStoredInB ? 1 : 0], histogramGroups, useBufferB);
		rd.ComputeListAddBarrier(computeList);
		useBufferB = !useBufferB;

		radixTrieShader.Bind(computeList, [particleCount], particleGroups, useBufferB);
		rd.ComputeListAddBarrier(computeList);

		switch (algorithm) {
			case 0:
				//naiveForcesShader.Bind(computeList, [(float)delta * timeScale, gravitationalConstant, epsilon * epsilon, particleCount], particleGroups, useBufferB);
				break;
			case 1:
				// rd.ComputeListBindComputePipeline(computeList, buildTreeShader.pipeline);
				// rd.ComputeListBindUniformSet(computeList, buildTreeShader.uniformSet, 0);
				// rd.ComputeListSetPushConstant(computeList, pushConstantsBytes, (uint)pushConstantsBytes.Length);
				// rd.ComputeListDispatch(computeList, groups, 1, 1);
				// rd.ComputeListAddBarrier(computeList);

				// rd.ComputeListBindComputePipeline(computeList, barnesHutCentersShader.pipeline);
				// rd.ComputeListBindUniformSet(computeList, barnesHutCentersShader.uniformSet, 0);
				// rd.ComputeListSetPushConstant(computeList, pushConstantsBytes, (uint)pushConstantsBytes.Length);
				// rd.ComputeListDispatch(computeList, groups, 1, 1);
				// rd.ComputeListAddBarrier(computeList);

				// rd.ComputeListBindComputePipeline(computeList, barnesHutForcesShader.pipeline);
				// rd.ComputeListBindUniformSet(computeList, barnesHutForcesShader.uniformSet, 0);
				// rd.ComputeListSetPushConstant(computeList, pushConstantsBytes, (uint)pushConstantsBytes.Length);
				// rd.ComputeListDispatch(computeList, groups, 1, 1);
				break;
		}

		//rd.ComputeListAddBarrier(computeList);
		integrationShader.Bind(computeList, [(float)delta * timeScale, textureWidth], particleGroups, useBufferB);

		mat.SetShaderParameter("camera_offset", cameraOffset);

		rd.ComputeListEnd();

		if (debugTimer != null && debugTimer.TimeLeft == 0) {
			DebugTimeout();
		}

		if (Input.IsActionJustPressed("reset")) {
			CreateNewSimulation();
		}
	}

	private void DebugTimeout() {
		if (DebugParticleProperties != 0) {
			if (debugBothBuffers) {
				DebugParticleBuffers(!useBufferB);
				DebugParticleBuffers(useBufferB);
			}
			else {
				DebugParticleBuffers();
			}
		}

		if (debugBounds) {
			DebugBoundsBuffers();
		}
	
		if (debugRadixSort) {
			DebugRadixSortBuffers();
		}

		if (debugZOrderCurve) {
			DebugZOrderCurve();
		}

		debugTimer.Start();
	}

	private void DebugParticleBuffers(bool useCurrentBuffer = true) {
		byte[] positionBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Position].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Position].bufferB);
		byte[] velocityBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Velocity].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Velocity].bufferB);
		byte[] accelerationBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Acceleration].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Acceleration].bufferB);
		byte[] massBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Mass].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Mass].bufferB);
		byte[] colorBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Color].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Color].bufferB);
		byte[] mortonBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.Morton].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.Morton].bufferB);
		byte[] radixIndexBytes = (useBufferB^useCurrentBuffer) ? rd.BufferGetData(transformBuffers[(int)TBN.RadixIndex].bufferA) : rd.BufferGetData(transformBuffers[(int)TBN.RadixIndex].bufferB);

		float[] positions = new float[particles.Length * 2];
		float[] velocities = new float[particles.Length * 2];
		float[] accelerations = new float[particles.Length * 2];
		float[] masses = new float[particles.Length];
		float[] colors = new float[particles.Length * 4];
		uint[] mortons = new uint[particles.Length];
		uint[] radixIndices = new uint[particles.Length];

		Buffer.BlockCopy(positionBytes, 0, positions, 0, positionBytes.Length);
		Buffer.BlockCopy(velocityBytes, 0, velocities, 0, velocityBytes.Length);
		Buffer.BlockCopy(accelerationBytes, 0, accelerations, 0, accelerationBytes.Length);
		Buffer.BlockCopy(massBytes, 0, masses, 0, massBytes.Length);
		Buffer.BlockCopy(colorBytes, 0, colors, 0, colorBytes.Length);
		Buffer.BlockCopy(mortonBytes, 0, mortons, 0, mortonBytes.Length);
		Buffer.BlockCopy(radixIndexBytes, 0, radixIndices, 0, radixIndexBytes.Length);

		var stringBuilder = new StringBuilder(positions.Length * 300);

		stringBuilder.Append("Buffer: ").Append((useBufferB ^ useCurrentBuffer) ? "A\n" : "B\n");
		uint lastMorton = 0;

		Godot.Collections.Dictionary<float, uint> previousRadixIndices = []; // RadixIndex, Index

		for (uint i = 0; i < particleCount; i++) {
			stringBuilder.Append('[').Append(i).Append("] ");
			if ((DebugParticleProperties & 1) != 0) {
				stringBuilder.Append("Position: (").Append(positions[i*2]).Append(", ").Append(positions[i*2 + 1]).Append(") ");
			}
			if ((DebugParticleProperties & 2) != 0) {
				stringBuilder.Append("Velocity: (").Append(velocities[i*2]).Append(", ").Append(velocities[i*2 + 1]).Append(") ");
			}
			if ((DebugParticleProperties & 4) != 0) {
				stringBuilder.Append("Acceleration: (").Append(accelerations[i*2]).Append(", ").Append(accelerations[i*2 + 1]).Append(") ");
			}
			if ((DebugParticleProperties & 8) != 0) {
				stringBuilder.Append("Mass: ").Append(masses[i]).Append(' ');
			}
			if ((DebugParticleProperties & 16) != 0) {
				stringBuilder.Append("Color: (").Append(colors[i*4 + 0]).Append(", ").Append(colors[i*4 + 1]).Append(", ").Append(colors[i*4 + 2]).Append(") ");
			}
			if ((DebugParticleProperties & 32) != 0) {
				stringBuilder.Append("Morton: ").Append(mortons[i]).Append(' ');
				
			}
			if ((DebugParticleProperties & 64) != 0) {
				stringBuilder.Append("Radix index: ").Append(radixIndices[i]).Append(' ');
			}

			if ((DebugParticleProperties & 128) != 0) {
				if (useCurrentBuffer && mortons[i] < lastMorton) {
					GD.PrintErr("Radix Sort Error: Unsorted Indices");
				}

				if (previousRadixIndices.ContainsKey(radixIndices[i])) {
					GD.Print("Radix Sort Error: Duplicate Indices (", previousRadixIndices[radixIndices[i]], ") and (", i, ") for index (", radixIndices[i], ")");
					continue;
				}

				previousRadixIndices.Add(radixIndices[i], i);
			}
			lastMorton = mortons[i];

			stringBuilder.Append('\n');
		}

		for (int i = 0; i < particleCount; i++) {
			if ((DebugParticleProperties & 128) != 0 && !previousRadixIndices.ContainsKey(i)) {
				GD.PrintErr("Missing index:", i);
			}
		}

		GD.Print("Particles: \n", stringBuilder.ToString());

		
	}

	private void DebugBoundsBuffers() {
		byte[] boundsBytes = rd.BufferGetData(boundsBuffer);
		float[] bounds = new float[boundsBytes.Length / sizeof(float)];
		Buffer.BlockCopy(boundsBytes, 0, bounds, 0, boundsBytes.Length);

		var stringBuilder = new StringBuilder(256);

		for (int i = 0; i < bounds.Length; i+= 4) {
			stringBuilder.Append("Max: ").Append(bounds[i]).Append(", ").Append(bounds[i+1]).Append('\n');
			stringBuilder.Append("Min: ").Append(bounds[i+2]).Append(", ").Append(bounds[i+3]).Append('\n');
		}
		

		GD.Print("Bounds: \n", stringBuilder.ToString());
	}

	private void DebugRadixSortBuffers() {
		byte[] histogramBytes = rd.BufferGetData(globalHistogramBuffer);
		byte[] scanBytes = rd.BufferGetData(scanBuffer);
		
		uint[] histogram = new uint[histogramBytes.Length / sizeof(uint)];
		uint[] scan = new uint[scanBytes.Length / sizeof(uint)];
		
		Buffer.BlockCopy(scanBytes, 0, scan, 0, scanBytes.Length);
		Buffer.BlockCopy(histogramBytes, 0, histogram, 0, histogramBytes.Length);
		
		var stringBuilder = new StringBuilder(histogram.Length * 32);

		for (int i = 0; i < histogram.Length; i++) {
			stringBuilder.Append("Digit: ").Append(i%radixBase).Append(", Count: ").Append(histogram[i]).Append(", Offset: ").Append(scan[i+radixBase]).Append('\n');
		}

		for (int i = 0; i < radixBase; i++) {
			stringBuilder.Append("Digit: ").Append(i).Append(", Global Offset: ").Append(scan[i]).Append('\n');
		}

		GD.Print("Radix Bits: " + radixBits + " Radix Base: " + radixBase + "\n" + stringBuilder.ToString());
	}

	private void DebugZOrderCurve() {
		byte[] positionBytes = useBufferB ? rd.BufferGetData(transformBuffers[(int)TBN.Position].bufferB) : rd.BufferGetData(transformBuffers[(int)TBN.Position].bufferA);
		byte[] mortonBytes = useBufferB ? rd.BufferGetData(transformBuffers[(int)TBN.Morton].bufferB) : rd.BufferGetData(transformBuffers[(int)TBN.Morton].bufferA);
		
		float[] positions = new float[particles.Length * 2];
		uint[] mortons = new uint[particles.Length];

		Buffer.BlockCopy(positionBytes, 0, positions, 0, positionBytes.Length);
		Buffer.BlockCopy(mortonBytes, 0, mortons, 0, mortonBytes.Length);

		zOrderLinePositions = new Vector2[particleCount];
		zOrderLineColors = new Color[particleCount];
		
		for (int i = 0; i < particleCount; i++) {
			zOrderLinePositions[i] = new Vector2(positions[2*i], positions[2*i+1]);
			zOrderLineColors[i] = ((mortons[i] >> 29) & 1) == 1 ? new(0.4f, 0.4f, 0.8f) : new(0.5f, 0.5f, 0.3f);
		}

		QueueRedraw();
	}

	public override void _Draw() {
		if (zOrderLinePositions == null) return;
		DrawPolylineColors(zOrderLinePositions, zOrderLineColors, 2.0f);
	}

	private byte[][] ParticlesToBytes() {
		Vector2[] positions = new Vector2[particles.Length];
		Vector2[] velocities = new Vector2[particles.Length];
		Vector2[] accelerations = new Vector2[particles.Length];
		float[] masses = new float[particles.Length];
		Vector4[] colors = new Vector4[particles.Length];
		uint[] mortons = new uint[particles.Length];
		uint[] radixIndices = new uint[particles.Length];

		for (uint i = 0; i < particles.Length; i++) {
			positions[i] = particles[i].position;
			velocities[i] = particles[i].velocity;
			accelerations[i] = particles[i].acceleration;
			masses[i] = particles[i].mass;
			colors[i] = particles[i].color;
			mortons[i] = 0;
			radixIndices[i] = i;
		}

		byte[] positionBytes = MemoryMarshal.Cast<Vector2, byte>(positions).ToArray();
		byte[] velocityBytes = MemoryMarshal.Cast<Vector2, byte>(velocities).ToArray();
		byte[] accelerationBytes = MemoryMarshal.Cast<Vector2, byte>(accelerations).ToArray();
		byte[] massBytes = MemoryMarshal.Cast<float, byte>(masses).ToArray();
		byte[] colorBytes = MemoryMarshal.Cast<Vector4, byte>(colors).ToArray();
		byte[] mortonBytes = MemoryMarshal.Cast<uint, byte>(mortons).ToArray();
		byte[] radixIndicesBytes = MemoryMarshal.Cast<uint, byte>(radixIndices).ToArray();

		return [positionBytes, velocityBytes, accelerationBytes, massBytes, colorBytes, mortonBytes, radixIndicesBytes];
	}

	private void FreeResources() {
		workgroupBoundsShader.Free();

		mortonEncodeShader.Free();

		radixHistogramShader.Free();
		radixWorkgroupScanShader.Free();
		radixGlobalScanShader.Free();
		radixIndexScatterShader.Free();
		radixFullScatterShader.Free();

		naiveForcesShader.Free();

		integrationShader.Free();

		foreach (TransformBuffer transformBuffer in transformBuffers) {
			transformBuffer.Free();
		}

		rd.FreeRid(boundsBuffer);
		rd.FreeRid(globalHistogramBuffer);
		rd.FreeRid(threadHistogramBuffer);
		rd.FreeRid(scanBuffer);

		rd.FreeRid(particleTransformTexture);
		rd.FreeRid(particleColorTexture);
	}

	public override void _ExitTree() {
		FreeResources();
	
		GD.Print("Freed shaders and buffers.");
		base._ExitTree();
	}
}
