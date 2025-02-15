using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkEngine : MeshInstance3D
{
	int[,,] _world = new int[80, 128, 80]; // Voxel data
	private FastNoiseLite terrainNoise;
	private FastNoiseLite caveNoise;

	private readonly Vector3[] faceOffsets = {
		new Vector3( 0,  0, -1),  // Front
		new Vector3( 0,  0,  1),  // Back
		new Vector3(-1,  0,  0),  // Left
		new Vector3( 1,  0,  0),  // Right
		new Vector3( 0,  1,  0),  // Top
		new Vector3( 0, -1,  0)   // Bottom
	};

	private readonly int[][] faceTriangles = {
		new int[] {0, 1, 2, 2, 3, 0}, // Front
		new int[] {0, 3, 2, 2, 1, 0}, // Back
		new int[] {0, 3, 2, 2, 1, 0}, // Left
		new int[] {0, 1, 2, 2, 3, 0}, // Right
		new int[] {0, 1, 2, 2, 3, 0}, // Top
		new int[] {0, 3, 2, 2, 1, 0}  // Bottom
	};


	private readonly Vector3[][] faceVertices = {
		new Vector3[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }, // Front
		new Vector3[] { new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1) }, // Back
		new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0) }, // Left
		new Vector3[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // Right
		new Vector3[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1) }, // Top
		new Vector3[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) }  // Bottom
	};

	public override void _Ready()
	{
		GD.Print("Initializing Chunk...");
		terrainNoise = new FastNoiseLite();
		terrainNoise.SetNoiseType(FastNoiseLite.NoiseTypeEnum.Perlin);
		terrainNoise.SetFrequency(0.02f);
		terrainNoise.SetSeed(43);

		caveNoise = new FastNoiseLite();
		caveNoise.SetNoiseType(FastNoiseLite.NoiseTypeEnum.Perlin);
		caveNoise.SetFrequency(0.05f);
		caveNoise.SetSeed(92);

		GenerateTerrain();
		UpdateMesh();
	}

	private void GenerateTerrain()
	{
		for (int x = 0; x < 80; x++)
		{
			for (int z = 0; z < 80; z++)
			{
				float heightValue = terrainNoise.GetNoise2D(x, z) * 20 + 96;
				int surfaceHeight = Mathf.Clamp((int)heightValue, 0, 127);

				for (int y = 0; y < 128; y++)
				{
					bool isSolid = y <= surfaceHeight;
					float caveValue = caveNoise.GetNoise3D(x, y, z);
					bool isCave = (caveValue > -0.035f && caveValue < 0.035f);

					if (isSolid && !isCave){
						_world[x, y, z] = 1;
					}else{
						_world[x, y, z] = 0;
					}
				}
			}
		}
	}
		
	private void UpdateMesh()
	{
		SurfaceTool st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
	
		List<Vector3> collisionVertices = new List<Vector3>(); // Store collision mesh data
	
		for (int x = 0; x < 80; x++)
		{
			for (int y = 0; y < 128; y++)
			{
				for (int z = 0; z < 80; z++)
				{
					if (_world[x, y, z] == 0)
						continue;
	
					AddBlockMesh(st, x, y, z, collisionVertices);
				}
			}
		}
	
		st.Index();
		Mesh = st.Commit();
	
		// Ensure material supports vertex colors
		StandardMaterial3D material = new StandardMaterial3D();
		material.VertexColorUseAsAlbedo = true;
		Mesh.SurfaceSetMaterial(0, material);
	
		// Generate and apply collision shape
		CreateCollisionShape(collisionVertices);	
	
	   	GD.Print("Voxel chunk generated with collisions.");
	}
	
	private void AddBlockMesh(SurfaceTool st, int x, int y, int z, List<Vector3> collisionVertices)
	{
		for (int i = 0; i < 6; i++)
		{
			Vector3 neighborPos = new Vector3(x, y, z) + faceOffsets[i];
			if (!IsSolid((int)neighborPos.X, (int)neighborPos.Y, (int)neighborPos.Z))
			{
				AddFace(st, x, y, z, i, collisionVertices);
			}
		}
	}
		
	private void AddFace(SurfaceTool st, int x, int y, int z, int faceIndex, List<Vector3> collisionVertices)
	{
		Vector3 basePos = new Vector3(x, y, z);
		Vector3[] vertices = faceVertices[faceIndex];
		int[] indices = faceTriangles[faceIndex];
		Vector3 normal = faceOffsets[faceIndex]; // Use face normal for lighting
	
		// Determine block type
		int blockType = _world[x, y, z];
		Vector2 uvOffset = GetUVForBlockType(blockType);
	
		// Define UV coordinates assuming a 16x16 texture atlas grid in a 256x256 texture
		float atlasSize = 16.0f; // 16x16 grid
		float uvSize = 1.0f / atlasSize;
	
		Vector2[] uvCoords = {
			uvOffset,
			uvOffset + new Vector2(uvSize, 0),
			uvOffset + new Vector2(uvSize, uvSize),
			uvOffset + new Vector2(0, uvSize)
		};
	
		for (int i = 0; i < 6; i++)
		{
			int idx = indices[i];
			st.SetUV(uvCoords[idx]);
			st.SetNormal(normal); // Apply normal for shading
			st.AddVertex(basePos + vertices[idx]);
			collisionVertices.Add(basePos + vertices[idx]);
		}
	}
	
	private Vector2 GetUVForBlockType(int blockType)
	{
		// Map block types to texture coordinates (assuming 16x16 blocks in a 256x256 texture)
		int textureX = blockType % 16;
		int textureY = blockType / 16;
		
		//GD.Print($"BlockType: {blockType}, TextureX: {textureX}, TextureY: {textureY}");

   		return new Vector2(textureX / 16.0f, textureY / 16.0f);
	}

	private void CreateCollisionShape(List<Vector3> collisionVertices)
	{
		if (collisionVertices.Count == 0)
			return;
	
		StaticBody3D staticBody = new StaticBody3D();
		AddChild(staticBody);
	
		CollisionShape3D collisionShape = new CollisionShape3D();
		staticBody.AddChild(collisionShape);
	
		ConcavePolygonShape3D shape = new ConcavePolygonShape3D();
		shape.SetFaces(collisionVertices.ToArray());
		
		collisionShape.Shape = shape;
	}

	private bool IsSolid(int x, int y, int z)
	{
		if (x < 0 || x >= 80 || y < 0 || y >= 128 || z < 0 || z >= 80)
			return false;
		return _world[x, y, z] != 0; // Now it considers all non-zero blocks as solid
	}
}
