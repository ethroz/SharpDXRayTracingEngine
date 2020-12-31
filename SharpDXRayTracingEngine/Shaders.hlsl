/////////////////////////////////////////
//            Declarations             //
/////////////////////////////////////////

struct Ray
{
	float3 origin;
	float3 direction;
};

/////////////////////////////////////////
//              Buffer                 //
/////////////////////////////////////////

cbuffer MainBuffer : register(b0)
{
	float3x3 EyeRot;
	float3 EyePos;
	float Width;
	float3 BGCol;
	float Height;
	float MinBrightness;
	float ModdedTime;
	uint RayCount;
	uint RayDepth;
	uint NumTris;
	uint NumSpheres;
	uint NumLights;
};

cbuffer TempBuffer : register(b1)
{
    float3 Vertices[1200];
    float4 Normals[1200];
    float4 Colors[400];
    float4 Spheres[3];
    float4 Lights[3];
};

/////////////////////////////////////////
//              Methods                //
/////////////////////////////////////////

float Square(float value)
{
	return value * value;
}

float rand(float2 p)
{
	float2 k = float2(
		23.14069263277926, // e^pi (Gelfond's constant)
		2.665144142690225 // 2^sqrt(2) (Gelfond–Schneider constant)
		);
	return frac(cos(dot(p, k)) * 12345.6789);
}

float random(float2 p)
{
	return rand(p.xy * (rand(p.xy * ModdedTime) - rand(rand(p.xy * ModdedTime) - ModdedTime)));
}

float3 refr(float3 I, float3 N, float ior)
{
	float cosi = dot(I, N);
	float etai = 1.0f;
	float etat = ior;
	float3 n = N;
	if (cosi < 0)
		cosi = -cosi;
	else
	{ 
		float temp = etai;
		etai = etat;
		etat = temp;
		n = -N;
	}
	float eta = etai / etat;
	float k = 1 - eta * eta * (1 - cosi * cosi);
	if (k < 0.0f)
		return reflect(I, n);
	else
		return eta * I + (eta * cosi - sqrt(k)) * n;
}

float TriangleInterpolation(float3 e, float3 f, float3 g)
{
	return length(cross(f, g)) / length(cross(f, e));
}

float TriangleIntersect(Ray ray, float3 v0, float3 v1, float3 v2)
{
	float3 n = normalize(cross(v1 - v0, v2 - v0));
	float numerator = dot(n, v0 - ray.origin);
	float denominator = dot(n, ray.direction);
	if (denominator >= 0.0f) // not facing camera
	{
		return -1.0f;
	}
	float intersection = numerator / denominator;
	if (intersection <= 0.0f) // intersects behind camera
	{
		return -1.0f;
	}

	// test if intersection is inside triangle ////////////////////////////
	float3 pt = ray.origin + ray.direction * intersection;
	float3 edge0 = v1 - v0;
	float3 edge1 = v2 - v1;
	float3 edge2 = v0 - v2;
	float3 C0 = pt - v0;
	float3 C1 = pt - v1;
	float3 C2 = pt - v2;
	if (!(dot(n, cross(C0, edge0)) <= 0 &&
		dot(n, cross(C1, edge1)) <= 0 &&
		dot(n, cross(C2, edge2)) <= 0))
	{
		return -1.0f; // point is outside the triangle
	}
	return intersection;
}

float SphereIntersect(Ray ray, float3 position, float radius, uint intersect)
{
	float3 toSphere = ray.origin - position;
	float discriminant = Square(dot(ray.direction, toSphere)) - dot(toSphere, toSphere) + Square(radius);
	if (discriminant < 0.0f) // does not intersect
	{
		return -1.0f;
	}
	float intersection = -dot(ray.direction, ray.origin - position);
	if (intersect == 1)
		intersection -= sqrt(discriminant);
	else if (intersect == 2)
		intersection += sqrt(discriminant);
	else
		return -1.0f;
	if (intersection <= 0.0f) // intersects behind camera
	{
		return -1.0f;
	}
	return intersection;
}

float3 RayCastEthan(Ray ray, uint itteration)
{
	float prevSpec = 1.0f;
    float distances[402];
	itteration++;
	for (uint j = 0; j < NumTris; j++)
	{
		distances[j] = TriangleIntersect(ray, Vertices[j * 3], Vertices[j * 3 + 1], Vertices[j * 3 + 2]);
	}
	for (j = 0; j < NumSpheres; j++)
	{
		distances[NumTris + j] = SphereIntersect(ray, Spheres[j * 2].xyz, Spheres[j * 2].w, 1);
	}
	for (j = 0; j < NumLights; j++)
	{
		distances[NumTris + NumSpheres + j] = SphereIntersect(ray, Lights[j * 3].xyz, Lights[j * 3 + 2].x, 1);
	}
	uint index = -1;
	float bestDistance = 1.#INF;
	for (j = 0; j < distances.Length; j++)
	{
		if (distances[j] != -1.0f && distances[j] < bestDistance)
		{
			index = j;
			bestDistance = distances[j];
		}
	}
	if (index != -1)
	{
		if (index >= NumTris + NumSpheres) // if light is closest
		{
			return Lights[(index - NumTris - NumSpheres) * 3 + 1].xyz * Lights[(index - NumTris - NumSpheres) * 3 + 2].y;
		}
		else // if sphere or tri is closest
		{
			ray.origin = ray.origin + ray.direction * bestDistance;
			float4 col;
			float3 normal;
			float spec = 0.0f;
			if (index >= NumTris) // if sphere is closest
			{
				col = Spheres[(index - NumTris) * 2 + 1];
				normal = normalize(ray.origin - Spheres[(index - NumTris) * 2].xyz);
			}
			else // if tri is closest
			{
				col = Colors[index];
				normal = Normals[index].xyz;
			}
			ray.direction = normalize(reflect(ray.direction, normal)); // reflect the ray off of the surface for the next raycast
			float3 color;
			for (uint k = 0; k < NumLights; k++)
			{
				float3 toLight = Lights[k * 3].xyz - ray.origin;
				if (dot(toLight, normal) > 0) // check if the surface is facing the light
				{
					Ray ray2;
					ray2.direction = normalize(toLight);
					ray2.origin = ray.origin;
					uint count = 0;
					for (uint m = 0; m < NumTris; m++)
					{
						float dist = TriangleIntersect(ray2, Vertices[m * 3], Vertices[m * 3 + 1], Vertices[m * 3 + 2]);
						if (dist != -1.0f && dist < length(toLight)) // check for shadows from tris
							count++;
					}
					for (m = 0; m < NumSpheres; m++)
					{
						float dist = SphereIntersect(ray2, Spheres[m * 2].xyz, Spheres[m * 2].w, 1);
						if (dist != -1.0f && dist < length(toLight)) // check for shadows from spheres
							count++;
					}
					if (count == 0) // check if there are no objects between the light and the sphere
					{
						float brightness = Lights[k * 3 + 2].y / 4.0f / 3.14f / dot(toLight, toLight);
						color += Lights[k * 3 + 1].xyz * col.xyz * max(lerp(0, brightness, prevSpec), MinBrightness);
						if (brightness > 1)
							spec += 1.0f;
						else
							spec += max(lerp(0, brightness, prevSpec), MinBrightness);
					}
					else
					{
						color += col.xyz * max(MinBrightness * prevSpec, MinBrightness);
						//spec += prevSpec;
						spec += MinBrightness;
					}
				}
				else
				{
					color += col.xyz * max(MinBrightness * prevSpec, MinBrightness);
					spec += MinBrightness;
				}
			}
			spec /= NumLights;
			prevSpec *= col.w * spec;
			return color;
		}
	}
	else
	{
		return BGCol * prevSpec;
		//color /= itteration;
		//return color;
	}
}

/////////////////////////////////////////
//        Shader Entry Points          //
/////////////////////////////////////////

float4 vertexShader(float4 position : POSITION) : SV_POSITION
{
    return position;
}

float3 pixelShader(float4 position : SV_POSITION) : SV_TARGET
{
	//return random(position.xy);
	//return float3(position.x / Width, position.y / Height, min((Width - position.x) / Width, (Height - position.y) / Height));

	// cross hair
	float2 p = position.xy;
	if (p.x > Width / 2 - 5 && p.x < Width / 2 + 5 && p.y > Height / 2 - 1 && p.y < Height / 2 + 1)
		return 1.0f;
	else if (p.y > Height / 2 - 5 && p.y < Height / 2 + 5 && p.x > Width / 2 - 1 && p.x < Width / 2 + 1)
		return 1.0f;

	float3 color = 0.0f;
	uint raycont;
    float distances[402];
    Ray rays[2];
	float dropOff[2];
	float pitch = (position.y * -2.0f / Height + 1.0f) * (Height / Width) * 0.1f;
	float yaw = (position.x * 2.0f / Width - 1.0f) * 0.1f;
	float3 direction = float3(yaw, pitch, 0.1f);
	direction = mul(EyeRot, direction);
	rays[0].origin = direction + EyePos;
	rays[0].direction = normalize(direction);
	//return RayCastEthan(ray, 1);

	dropOff[0] = 1.0f;
	uint count = 1;
	for (uint i = 0; i < RayDepth; i++)
	{
		for (uint j = 0; j < count; j++)
		{
			if (rays[j].direction.x * rays[j].direction.x + rays[j].direction.y * rays[j].direction.y + rays[j].direction.z * rays[j].direction.z == 0.0f)
				break;
			raycont++;
			for (uint k = 0; k < NumTris; k++)
			{
				distances[k] = TriangleIntersect(rays[j], Vertices[k * 3], Vertices[k * 3 + 1], Vertices[k * 3 + 2]);
			}
			for (k = 0; k < NumSpheres; k++)
			{
				distances[NumTris + k] = SphereIntersect(rays[j], Spheres[k * 3].xyz, Spheres[k * 3].w, 1);
			}
			for (k = 0; k < NumLights; k++)
			{
				distances[NumTris + NumSpheres + k] = SphereIntersect(rays[j], Lights[k * 3].xyz, Lights[k * 3 + 2].x, 1);
			}
			uint index = -1;
			float bestDistance = 1.#INF;
			for (k = 0; k < distances.Length; k++)
			{
				if (distances[k] != -1.0f && distances[k] < bestDistance)
				{
					index = k;
					bestDistance = distances[k];
				}
			}
			if (index != -1)
			{
				if (index >= NumTris + NumSpheres) // if light is closest
				{
					color += Lights[(index - NumTris - NumSpheres) * 3 + 1].xyz * Lights[(index - NumTris - NumSpheres) * 3 + 2].y;
					rays[j].direction = 0.0f;
				}
				else // if sphere or tri is closest
				{
					rays[j].origin = rays[j].origin + rays[j].direction * bestDistance;
					direction = -rays[j].direction;
					float4 col;
					float3 normal;
					if (index >= NumTris) // if sphere is closest, normal is from centre of sphere to intersect
					{
						col = Spheres[(index - NumTris) * 3 + 1];
						normal = normalize(rays[j].origin - Spheres[(index - NumTris) * 3].xyz);
					}
					else // if tri is closest, blend between each vertex normal
					{
						col = Colors[index];
						uint4 indices = uint4(0, 1, 2, 0);
						if (Normals[index * 3].w == 1.0f) // if the triangle is special needs
							indices = uint4(1, 2, 0, 1);
						else if (Normals[index * 3 + 1].w == 1.0f)
							indices = uint4(0, 2, 1, 1);
						else if (Normals[index * 3 + 2].w == 1.0f)
							indices.w = 1;
						Ray a;
						Ray b;
						a.origin = Vertices[index * 3 + indices.x];
						a.direction = Vertices[index * 3 + indices.y] - Vertices[index * 3 + indices.x];
						b.origin = Vertices[index * 3 + indices.z];
						b.direction = rays[j].origin - Vertices[index * 3 + indices.z];
						float c = TriangleInterpolation(a.direction, b.direction, Vertices[index * 3 + indices.z] - Vertices[index * 3 + indices.x]);
						float3 intersect = a.origin + a.direction * c;
						float d = length(b.direction) / length(intersect - Vertices[index * 3 + indices.z]);
						normal = normalize(lerp(Normals[index * 3 + indices.x].xyz, Normals[index * 3 + indices.y].xyz, c));
						if (indices.w == 0)
							normal = normalize(lerp(Normals[index * 3 + indices.z].xyz, normal, d));
					}
					rays[j].direction = normalize(reflect(rays[j].direction, normal));
					color += col.xyz * MinBrightness; // ambient light
					for (k = 0; k < NumLights; k++)
					{
						float3 toLight = Lights[k * 3].xyz - rays[j].origin;
						if (dot(toLight, normal) > 0) // check if the surface is facing the light
						{
							float dist = length(toLight);
							Ray ray2;
							ray2.direction = normalize(toLight);
							ray2.origin = rays[j].origin;
							uint number = 0;
							float mult = 1.0f;
							bool dim = false;
							for (uint m = 0; m < NumTris; m++)
							{
								float d = TriangleIntersect(ray2, Vertices[m * 3], Vertices[m * 3 + 1], Vertices[m * 3 + 2]);
								if (d != -1.0f && d < dist) // check for shadows from tris
									number++;
							}
							for (m = 0; m < NumSpheres; m++)
							{
								float d = SphereIntersect(ray2, Spheres[m * 3].xyz, Spheres[m * 3].w, 1);
								if (d != -1.0f && d < dist)// && Spheres[m * 3 + 2].x == 1.#INF) // check for shadows from spheres
									number++;
								if (d != -1.0f && Spheres[m * 3 + 2].x != 0.0f) // check if intersecting sphere is transparent
								{
									dim = true;
									mult *= rcp(abs(Spheres[m * 3 + 2].x));
								}
							}
							if (number == 0 || dim) // check if there are no objects between the light and the sphere
							{
								float brightness = Lights[k * 3 + 2].y / dot(toLight, toLight);
								color += Lights[k * 3 + 1].xyz * col.xyz * brightness * saturate(dot(ray2.direction, normal)) * mult * dropOff[j]; // diffused light
								//float3 H = normalize(normalize(toLight) + direction);
								//color += pow(saturate(dot(normal, H)), pow(2, col.w * 14 + 2.0f)) * mult * dropOff[j];  												// blinn-phong specular light
								color += pow(saturate(dot(rays[j].direction, normalize(toLight))), pow(2, col.w * 12.0f + 1.0f)) * mult * dropOff[j] * brightness;	// phong specular light
							}
						}
					}
					dropOff[j] *= col.w;
					if (dropOff[j] <= MinBrightness)
						rays[j].direction = 0.0f;
					if (index >= NumTris)
					{
						if (Spheres[(index - NumTris) * 3 + 2].x != 0.0f)
						{
							// refract ray
							if (Spheres[(index - NumTris) * 3 + 2].x == 1.0f)
								raycont--;
							rays[count].origin = rays[j].origin;
							rays[count].direction = -direction;
							rays[count].direction = refr(rays[count].direction, normal, Spheres[(index - NumTris) * 3 + 2].x);
							float intersect = SphereIntersect(rays[count], Spheres[(index - NumTris) * 3].xyz, Spheres[(index - NumTris) * 3].w, 2);
							rays[count].origin = rays[count].origin + rays[count].direction * intersect;
							float3 normal2 = normalize(rays[count].origin - Spheres[(index - NumTris) * 3].xyz);
							rays[count].direction = refr(rays[count].direction, normal2, Spheres[(index - NumTris) * 3 + 2].x);
							dropOff[count] = dropOff[j];
							count++;
						}
					}
				}
			}
			else
			{
				color += BGCol;
				rays[j].direction = 0.0f;
				if (j > 0)
					raycont++;
				/*if (raycont > 1)
					raycont -= (0.2126f * (1.0f - BGCol.r) + 0.7152f * (1.0f - BGCol.g) + 0.0722f * (1.0f - BGCol.b));*/
			}
		}
	}
	color /= raycont;
	color += ((random(position.xy) - 0.5f) / 255.0f);
	return color;
}
