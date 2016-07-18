using UnityEngine;
using System;
using System.Collections;
using System.Diagnostics;

struct Tile{
	private int elev; //max set to 6000 m, add mountains later
	private double temp; //range roughly -3 to 29
	private double moisture; //range 0 to 400 cm/year
	private bool land;
	private int river;
	private int lake;
	private int biome;
	
	public void SetLand(bool isLand){
		land = isLand;
		if (isLand) river = -1;
		else river = 0;
	}
	
	public bool GetLand(){
		return land;
	}
	
	public void SetBiome(int newBiome){
		biome = newBiome;
	}
	
	public int GetBiome(){
		return biome;
	}
	
	public void SetElev(int newElev){
		elev = newElev;
	}
	
	public int GetElev(){
		return elev;
	}
	
	public void SetTemp(double newTemp){
		temp = newTemp;
	}
	
	public double GetTemp(){
		return temp;
	}
	
	public void SetRiver(int id){
		river = id;
		biome = 6;
	}
	
	public int GetRiver(){
		return river;
	}
	
	public void SetMoisture(double val){
		moisture = val;
	}
	
	public double GetMoisture(){
		return moisture;
	}
}

struct Map{
	private Tile[,] map;
	private int width;
	private int height;
	private int size;
	private System.Random rnd;
	
	public Map(int w, int h, System.Random rand){
		width = w;
		height = h;
		size = w/4;
		map = new Tile[w, h];
		rnd = rand;
	}
	
	public void GenerateLandMass(){
		
		double [,] values = HexTonic(width,height,rnd);
		double [,] temp = PerlinTemp(width,height,rnd,values);
		values = SmoothMap(values);
		double seaLevel = SeaLevel(values);
		//seaLevel = 0;
		int elev;
		UnityEngine.Debug.Log("Sea Level: " + seaLevel);
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (values[x,y] >= seaLevel){
					map[x,y].SetLand(true);
					
				}
				else map[x,y].SetLand(false);
				elev = (int)((values[x,y]-seaLevel)/(1-seaLevel)*6000);
				map[x,y].SetElev(elev);
			}
		}
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				map[x,y].SetTemp(temp[x,y]-(double)map[x,y].GetElev()/(double)2000);
			}
		}
	}
	
	public double[,] PerlinTemp(int width, int height, System.Random rand, double[,] map){
		double [,] newmap = new Double[width,height];
		Perlin perlin = new Perlin(rand);
		float zoom = 40.0f;
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				newmap[x,y] = 26.0*Math.Pow(Math.Cos((y-height/2)*Math.PI/(height*1.1)),2) + 3*perlin.Noise(x/zoom,y/zoom);
			}
		}
		return newmap;
	}
	
	public bool NearbyRivers(int x, int y){
		for (int i = -5; i < 5; i++){
			for (int j = -5; j < 5; j++){
				if (x+i >= 0
					&& x+i < width
					&& y+j >=0
					&& y+j < height && map[x+i,y+j].GetRiver()>0) return true;
			}
		}
		return false;
	}
	
	public void AddRiverSources(int id){
		int [] heights = new int[0];
		int possible_locations = 0;
		int start;
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (map[x,y].GetElev() > 2000 && !NearbyRivers(x,y) && rnd.NextDouble() < 0.5){
					heights = AddInt(heights, possible_locations, y*width+x);
					possible_locations++;
				}
			}
		}
		if (possible_locations!=0){
			start = heights[rnd.Next(possible_locations)];
			AddRiverTile(map,width,height,start%width,start/width,id);
			AddRiverSources(id+1);
		}
	}
	
	public void AddRiverTile(Tile [,] newmap, int width, int height, int x, int y, int id){
		newmap[x,y].SetRiver(id);
		double min = 0;
		double correctedmin = 0;
		int new_x = x;
		int new_y = y;
		bool initialized = false;
		for (int i = -1; i < 2; i++){
			for (int j = -1; j < 2; j++){
				if (x+i >= 0
					&& x+i < width
					&& y+j >=0
					&& y+j < height
					&& newmap[x+i,y+j].GetRiver() != id){ //don't build a river tile where there's already a river of the same ID
						if (!initialized || (newmap[x+i,y+j].GetElev()-newmap[x,y].GetElev())/Math.Sqrt(i*i+j*j) <= min){
							initialized = true;
							new_x = x+i;
							new_y = y+j;
							min = (newmap[new_x,new_y].GetElev()-newmap[x,y].GetElev())/Math.Sqrt(i*i+j*j); //Add a correction for not preferring diagonals when eroding
							if (newmap[new_x,new_y].GetRiver() != -1) return; //already a river or ocean
						}
					}
			}
		}
		if (initialized){
			if (min <= 0) AddRiverTile(newmap,width,height,new_x,new_y,id);
			else{
				newmap[new_x,new_y].SetElev(newmap[x,y].GetElev()-4);
				AddRiverTile(newmap,width,height,new_x,new_y,id);
			}
		}
		else AddRiverTile(newmap,width,height,x+2*rnd.Next(-1,2),y+2*rnd.Next(-1,2),id);
	}
	
	public void WaterPass(){
		int newelev;
		int count;
		int rivercount;
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				newelev = 0;
				count = 0;
				for (int i = -1; i < 2; i++){
					for (int j = -1; j < 2; j++){
						//UnityEngine.Debug.Log(x+i);
						if ((x+i)>=0 && (x+i) < width && (y+j)>=0 && y+j < height){
							newelev += map[x+i,y+j].GetElev();
							count++;
						}
					}
				}
				map[x,y].SetElev(newelev/count);
				if (map[x,y].GetRiver() == -1){
					if (!(x==0 || y==0 || x==width-1 || x==height-1)){
						if (map[x-1,y].GetRiver() > 0
							&& map[x+1,y].GetRiver() > 0
							&& map[x,y-1].GetRiver() > 0
							&& map[x,y+1].GetRiver() > 0){
							map[x,y].SetRiver(map[x-1,y].GetRiver());
							//UnityEngine.Debug.Log("Found a lake tile at " + x + ", " + y);
						}
					}
				}
			}
		}
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (map[x,y].GetRiver() > 0){
					count = 0;
					for (int i = -1; i < 2; i++){
						for (int j = -1; j < 2; j++){
							if ((x+i)>=0 && (x+i) < width && (y+j)>=0 && y+j < height){
								if (map[x+i,y+j].GetRiver() > 0) count++;
							}
						}
					}
					//if (count>=4) UnityEngine.Debug.Log("Found a lake tile at " + x + ", " + y);
				}
			}
		}
	}
	
	public void MoistureCalc(){
		double [,] values = new double[width,height];
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				values[x,y]=0;
			}
		}
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				for (int i = 0; i < 10; i++){
					for (int j = 0; j < 10; j++){
						if (map[x,y].GetRiver()>=0){
							if (x-i >= 0 && y-j >=0){
								values[x-i,y-j]+=(1/Math.Sqrt(1+i*i+j*j));
							}
							if (x-i >= 0 && y+j < height){
								values[x-i,y+j]+=(1/Math.Sqrt(1+i*i+j*j));
							}
							if (x+i < width && y-j >=0){
								values[x+i,y-j]+=(1/Math.Sqrt(1+i*i+j*j));
							}
							if (x+i < width && y+j < height){
								values[x+i,y+j]+=(1/Math.Sqrt(1+i*i+j*j));
							}
						}
					}
				}
			}
		}
		
		//normalize to between 0 and 400 cm/year
		
		double max = 0;
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (values[x,y] > max && map[x,y].GetRiver()!=0) max = values[x,y];
			}
		}
		
		//add values to moisture
		
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				map[x,y].SetMoisture(400*values[x,y]/max);
			}
		}
	}
	
	public int[] AddInt(int [] list, int size, int newint){
		int [] newlist = new int[size+1];
		for (int i = 0; i < size; i++){
			newlist[i] = list[i];
		}
		newlist[size] = newint;
		return newlist;
	}
	
	private int FocusDistance(int focus, int x, int y){
		int f1 = width/2-focus;
		int f2 = width/2+focus;
		int dist1 = (x-f1)*(x-f1)+(y-height/2)*(y-height/2);
		int dist2 = (x-f2)*(x-f2)+(y-height/2)*(y-height/2);
		if (dist1 > dist2) return (int)Math.Sqrt(dist2);
		else return (int)Math.Sqrt(dist1);
	}
	
	public void SetBiomes(){
		for(int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (map[x,y].GetRiver()==0) map[x,y].SetBiome(5);
				else if (map[x,y].GetRiver()>0) map[x,y].SetBiome(6);
				else if (map[x,y].GetElev()<400) map[x,y].SetBiome(0);
				else {
					if (map[x,y].GetMoisture()<50) map[x,y].SetBiome(1);
					else if (map[x,y].GetMoisture()<150){
						if (map[x,y].GetTemp()<24) map[x,y].SetBiome(3);
						else map[x,y].SetBiome(2);
					}
					else map[x,y].SetBiome(2);
					if (map[x,y].GetElev()>4000) map[x,y].SetBiome(4);
				}
			}
		}
	}
	
	public void RandomBiomes(){
		System.Random rnd = new System.Random();
		int rnd_biome;
		double temp;
		for(int x = 0; x < width; x++){
			for(int y = 0; y < height; y++){
				if (map[x,y].GetElev() <= 0) map[x,y].SetBiome(5);
				else if (map[x,y].GetElev() < 500){
					map[x,y].SetBiome(0);
				}
				else if (map[x,y].GetElev() < 1000){
					map[x,y].SetBiome(1);
				}
				else if (map[x,y].GetElev() < 1500){
					map[x,y].SetBiome(3);
				}
				else if (map[x,y].GetElev() < 2000){
					map[x,y].SetBiome(2);
				}
				else map[x,y].SetBiome(4);
			}
		}
		/*for(int x = 0; x < width; x++){
			for(int y = 0; y < height; y++){
				temp = (map[x,y].GetTemp());
				if (temp > 25) map[x,y].SetBiome(4);
				else if (temp > 20) map[x,y].SetBiome(2);
				else if (temp > 15) map[x,y].SetBiome(3);
				else if (temp > 10) map[x,y].SetBiome(1);
				else if (temp > 5) map[x,y].SetBiome(0);
				else map[x,y].SetBiome(5);
			}
		}*/
	}

	public int TileBiome(int x, int y){
		return map[x,y].GetBiome();
	}
	public double SeaLevel(double[,] heightmap){
		double seapercent = 0.5 + 0.3*rnd.NextDouble();
		UnityEngine.Debug.Log("Sea Coverage: " + seapercent*100 + "%");
		bool [,] array = new bool[width,height];
		double min = heightmap[0,0];
		Vector2 minind = new Vector2(0,0);
		double max = heightmap[0,0];
		Vector2 maxind = new Vector2(0,0);
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				if (heightmap[x,y] < min){
					minind.x = x;
					minind.y = y;
					min = heightmap[x,y];
				}
				if (heightmap[x,y] > max){
					maxind.x = x;
					maxind.y = y;
					max = heightmap[x,y];
				}
				array[x,y] = false;
			}
		}
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				heightmap[x,y] = (heightmap[x,y]-min)/(max-min);
			}
		}
		int count = 0;
		double e = 0.05;
		
		while((double)count/((double)(width*height)) < seapercent){
			e = e+0.05;
			for (int x = 0; x < width; x++){
				for (int y = 0; y < height; y++){
					if(!array[x,y] && heightmap[x,y] < e){
						array[x,y] = true;
						count++;
					}
				}
			}
		}
		
		return e;
	}
	public double[,] DiamondSquare(System.Random rand){
		double [,] heightmap = new double[129,129];
		int step = 7;
		double factor = 0.8+rand.NextDouble();
		heightmap[0,0] = -step*rand.NextDouble();
		heightmap[0,128] = -step*rand.NextDouble();
		heightmap[128,0] = -step*rand.NextDouble();
		heightmap[128,128] = -step*rand.NextDouble();
		step--;
		int dist;
		while(step>=0){
			dist = (int)Math.Pow(2,step);
			for (int x = dist; x <= 128; x+=dist*2){
				for (int y = dist; y <= 128; y+=dist*2){
					heightmap[x,y] = SquareSample(heightmap, x, y, dist) + factor*rand.Next(-1,1)*step*rand.NextDouble();
					if (step == 6){
						heightmap[x,y] = -heightmap[x,y];
					}
					//UnityEngine.Debug.Log("Square Sample for " + x + ", " + y + ": " + heightmap[x,y]);
				}
			}
			for (int x = 0; x <= 128; x+=dist*2){
				for (int y = dist; y <= 128; y+=dist*2){
					//UnityEngine.Debug.Log("x = " + x + ", y = " + y + ", dist = " + dist);
					heightmap[x,y] = DiamondSample(heightmap, x, y, dist) + factor*rand.Next(-1,1)*step*rand.NextDouble();
					//UnityEngine.Debug.Log("Diamond Sample for " + x + ", " + y + ": " + heightmap[x,y]);
					heightmap[y,x] = DiamondSample(heightmap, y, x, dist) + factor*rand.Next(-1,1)*step*rand.NextDouble();
					//UnityEngine.Debug.Log("Diamond Sample for " + y + ", " + x + ": " + heightmap[y,x]);
				}
			}
			step--;
		}
		for (int x = 0; x < 129; x++){
			for (int y = 0; y < 129; y++){
				heightmap[x,y] += 2*Math.Cos((double)Math.Sqrt((double)((y-64)*(y-64)+(x-64)*(x-64)))*2*Math.PI/129); //normalizes height somewhat to continent
			}
		}
		return heightmap;
	}
	
	private double DiamondSample(double[,] array, int x, int y, int dist){
		double total = 0;
		if (x>0 && y>0 && x<128 && y<128) {
			total = (array[x-dist,y] + array[x+dist, y] + array[x, y-dist] + array[x, y+dist])/4.0;
			//UnityEngine.Debug.Log(total + " " + array[x-dist,y] + " " +  array[x+dist, y] + " " +  array[x, y-dist] + " " + array[x, y+dist]);
		}
		else if (x==0) total = (total + array[x+dist, y] + array[x, y-dist] + array[x, y+dist])/3.0;
		else if (x==128) total = (total + array[x-dist,y] + array[x, y-dist] + array[x, y+dist])/3.0;
		else if (y==0) total = (total + array[x-dist,y] + array[x+dist, y] + array[x, y+dist])/3.0;
		else if (y==128) total = (total + array[x-dist,y] + array[x+dist, y] + array[x, y-dist])/3.0;
		return total;
	}
	private double SquareSample(double[,] array, int x, int y, int dist){
		double total = 0;
		total = total + array[x-dist,y-dist] + array[x-dist,y+dist] + array[x+dist,y-dist] + array[x+dist,y+dist];
		return total/(double)4.0;
	}
	
	public Vector2 [] AddVector(Vector2 [] list, int size, Vector2 vector){
		Vector2 [] newlist = new Vector2[size+1];
		for (int i = 0; i < size; i++){
			newlist[i] = list[i];
		}
		newlist[size] = vector;
		return newlist;
	}
	
	public Edge [] AddEdge(Edge [] list, int size, Edge edge){
		Edge [] newlist = new Edge[size+1];
		for (int i = 0; i < size; i++){
			newlist[i] = list[i];
		}
		newlist[size] = edge;
		return newlist;
	}
	
	public int ContainsEdge(Edge [] list, int size, Edge edge){
		for (int i = 0; i < size; i++){
			if (list[i].Equals(edge)) return i;
		}
		//UnityEngine.Debug.Log("Found a duplicate edge");
		return -1;
	}
	
	public int ContainsVector(Vector2 [] list, int size, Vector2 vector){
		for (int i = 0; i < size; i++){
			if (Math.Abs(list[i].x-vector.x) < 0.4 && Math.Abs(list[i].y-vector.y) < 0.4) return i;
		}
		return -1;
	}
	
	public HexPlate[] AddPlate(HexPlate [] list, int size, HexPlate newplate){
		HexPlate [] newlist = new HexPlate[size+1];
		for (int i = 0; i < size; i++){
			newlist[i] = list[i];
		}
		newlist[size] = newplate;
		return newlist;
	}
	
	public double[,] HexTonic(int w, int h, System.Random rand){
		int num_plates = 0;
		int num_verts = 0;
		int num_edges = 0;
		int newind;
		double [,] newmap = new double[w,h];
		for (int x = 0; x < w; x++){
			for (int y = 0; y < h; y++){
				newmap[x,y] = 0;
			}
		}
		int horiz_hexes = w/30;
		double hex_width = (double)(w/horiz_hexes);
		double hex_height = (double)(hex_width*2/Math.Sqrt(3));
		int vert_hexes = (int)(h/hex_height + 1);
		Edge [] edges = new Edge[0];
		Edge tempedge;
		Vector2 [] coords = new Vector2[0];
		Vector2 newcenter;
		Vector2 [] freshverts;
		int [] freshinds;
		int [] freshedges;
		double weight;
		HexPlate [] plates = new HexPlate[0];
		for (int j = 0; j < vert_hexes; j++){
			for (int i = 0; i < horiz_hexes+1; i++){
				plates = AddPlate(plates, num_plates, new HexPlate(rand));
				plates[num_plates].RandomizeDirection();
				plates[num_plates].RandomizeOcean();
				if (i == 0 || j ==0 || (j%2==0 && i == horiz_hexes-1) || i == horiz_hexes || j == vert_hexes-1) plates[num_plates].SetOcean(true);
				num_plates++;
				if (j%2==0){
					newcenter.x = (float)(i*hex_width);
					newcenter.y = (float)(j*hex_height);
					plates[num_plates-1].SetCenter(newcenter);
					/*if (newcenter.y < h && newcenter.x < w) newmap[(int)newcenter.x,(int)newcenter.y] = 4.0;*/
				}
				else{
					newcenter.x = (float)(i*hex_width+hex_width/2);
					newcenter.y = (float)(j*hex_height);
					plates[num_plates-1].SetCenter(newcenter);
					/*if (newcenter.y < h && newcenter.x < w) newmap[(int)newcenter.x,(int)newcenter.y] = 4.0;*/
				}
				freshinds = new int[6];
				freshverts = FreshVerts(newcenter,hex_width,hex_height);
				for (int k = 0; k < 6; k++){
					newind = ContainsVector(coords, num_verts, freshverts[k]);
					if (newind == -1){
						coords = AddVector(coords, num_verts, freshverts[k]);
						freshinds[k] = num_verts;
						num_verts++;
					}
					else{
						freshinds[k] = newind;
					}
				}
				plates[num_plates-1].SetVertices(freshinds);
			}
		}
		
		coords = RandomizeVerts(coords,num_verts,hex_width,hex_height,rand);
		
		//adds edges to indexed list of edges, adds list of 6 edges to each plate
		for (int i = 0; i < num_plates; i++){
			freshinds = plates[i].GetVertices();
			freshedges = new int[6];
			
			for (int j = 0; j < 6; j++){
				tempedge = new Edge();
				tempedge.SetVectors(coords[freshinds[j]],coords[freshinds[((j+1)%6)]],rand);
				newind = ContainsEdge(edges, num_edges, tempedge);
				if (newind == -1){
					edges = AddEdge(edges, num_edges, tempedge);
					edges[num_edges].AddPlate(i);
					freshedges[j]=num_edges;
					num_edges++;
					//tempedge.DrawEdge(newmap,w,h);
				}
				else {
					freshedges[j] = newind;
					edges[newind].AddPlate(i);
				}
			}			
		}
		HexPlate plate0;
		HexPlate plate1;
		int [] edge_plates;
		for (int i = 0; i < num_edges; i++){
			if (edges[i].num_plates == 2){
				edge_plates = edges[i].GetPlates();
				plate0 = plates[edge_plates[0]];
				plate1 = plates[edge_plates[1]];
				if (plate0.GetOcean()!=plate1.GetOcean()){
					plate0.opposite_borders ++;
					plate1.opposite_borders ++;
				}
				edges[i].SetWeight(CrossWeight(plate0.GetCenter(),plate1.GetCenter(),plate0.GetDirection(),plate1.GetDirection()));
				edges[i].SetDisplacement(MidPoint());
				edges[i].DrawEdge(newmap,w,h);
			}
		}
		
		for (int i = 0; i < num_plates; i++){
			DrawPlate(newmap,w,h,plates[i],coords);
		}
		
		for (int i = 0; i < num_verts; i++){
			if(coords[i].x > 0 && coords[i].x < w && coords[i].y > 0 && coords[i].y < h){
				/*newmap[(int)coords[i].x,(int)coords[i].y] = 2.0;*/
			}
		}
		return newmap;
	}
	
	public double[] MidPoint(){
		double [] displacement = new double[33];
		displacement[0] = 0;
		displacement[32] = 0;
		int dist;
		for (int i = 4; i >= 0; i--){
			dist = (int)Math.Pow(2,i);
			for (int j = dist; j < 33; j += dist*2){
				displacement[j] = (displacement[j-dist]+displacement[j+dist])/2 + dist*rnd.Next(-1,1)*rnd.NextDouble();
			}
		}
		
		return displacement;
	}
	//TODO: FINISH THIS (draw full shape of plate with noise, not just lines)
	public void DrawPlate(double[,] newmap, int w, int h, HexPlate plate, Vector2[] verts){
		double weight = plate.GetOcean() ? -1 : 1;
		weight = weight * (7-plate.opposite_borders)/3;
		Vector2 center = plate.GetCenter();
		Vector2[] vertices = new Vector2[6];
		int area = 0;
		Vector2[] newdraws = new Vector2[0];
		for (int i = 0; i < 6; i++){
			vertices[i] = verts[plate.GetVertices()[i]];
		}
		
		for (int i = 0; i < 6; i++){
			vertices[i].x -= center.x;
			vertices[i].y -= center.y;
		}
		int new_x;
		int new_y;
		
		
		double max_distance = MaxDistance(vertices);
		for (int i = (int)Math.Floor(max_distance); i >= 0; i--){
			AddLines(vertices, ref newdraws, (double)i/((max_distance+1)), ref area);
		}
		for (int i = 0; i < area; i++){
			new_x = (int)(center.x+newdraws[i].x);
			new_y = (int)(center.y+newdraws[i].y);
			if(new_x >= 0 && new_y >= 0 && new_x < w && new_y < h){
				newmap[new_x,new_y] += (weight);
			}
		}
		/*for (int x = -hex_width; x <=hex_width; x++){
			for (int y = -hex_height; y <=hex_height; y++){
				if(center.x + x >= 0 && center.x + x < w && center.y + y >= 0 && center.y + y < h){
					newmap[(int)center.x + x,(int)center.y + y] += (weight*Math.Cos(Math.Sqrt(x*x+y*y)*Math.PI/((Math.Sqrt(x*x+y*y)/)*2)));
				}
			}
		}*/
	}
	//public double[,] PerlinMult()
	
	public void AddLines(Vector2[] vertices, ref Vector2[] newarea, double multiplier, ref int area){
		Vector2[] newvertices = new Vector2[6];
		for (int i = 0; i < 6; i++){
			newvertices[i].x = vertices[i].x*(float)multiplier;
			newvertices[i].y = vertices[i].y*(float)multiplier;
		}
		for (int i = 0; i < 6; i++){
			DrawLine(ref newarea,ref area,newvertices[i],newvertices[(i+1)%6]);
		}
	}
	
	public void DrawLine(ref Vector2[] newarea, ref int area, Vector2 vector1, Vector2 vector2){
		double distance = Math.Sqrt(Math.Pow((vector2.x-vector1.x),2)+Math.Pow((vector2.y-vector1.y),2));
		double unit_x = (vector2.x-vector1.x)/distance;
		double unit_y = (vector2.y-vector1.y)/distance;
		float add_x;
		float add_y;
		for (int i = 0; i < distance; i++){
			add_x = (float)((vector1.x+unit_x*i)%1 <= 0.5 ? Math.Floor(vector1.x+unit_x*i) : Math.Ceiling(vector1.x+unit_x*i));
			add_y = (float)((vector1.y+unit_y*i)%1 <= 0.5 ? Math.Floor(vector1.y+unit_y*i) : Math.Ceiling(vector1.y+unit_y*i));
			if (ContainsVector(newarea,area,new Vector2(add_x,add_y))==-1){
				newarea = AddVector(newarea,area,new Vector2(add_x,add_y));
				area++;
			}
		}
	}
	
	public double MaxDistance(Vector2[] vertices){
		double max = 0;
		double check;
		for (int i = 0; i < 6; i++){
			for (int j = i; j < 6; j++){
				check = Math.Sqrt(Math.Pow(vertices[i].x-vertices[j].x,2)+Math.Pow(vertices[i].y-vertices[j].y,2));
				if (check > max) max = check;
			}
		}
		return max;
	}
	
	public double CrossWeight(Vector2 vert1, Vector2 vert2, Vector2 dir1, Vector2 dir2){
		Vector2 direction = vert2-vert1;
		double length = Math.Sqrt(Math.Pow(direction.x,2) + Math.Pow(direction.y,2));
		direction.x = (float)(direction.x / length);
		direction.y = (float)(direction.y / length);
		Vector2 perp_dir;
		perp_dir.x = direction.y;
		perp_dir.y = -direction.x;
		double cross1 = dir1.x*direction.y-dir1.y*direction.x;
		double cross2 = dir2.x*direction.y-dir2.y*direction.x;
		return 4*cross1*cross2;
	}
	
	public Vector2[] FreshVerts(Vector2 coords, double hex_width, double hex_height){
		Vector2[] list = new Vector2[6];
		list[0] = new Vector2((float)coords.x,(float)(coords.y+hex_height*2/3));
		list[1] = new Vector2((float)(coords.x+hex_width/2),(float)(coords.y+hex_height/3));
		list[2] = new Vector2((float)(coords.x+hex_width/2),(float)(coords.y-hex_height/3));
		list[3] = new Vector2((float)coords.x,(float)(coords.y-hex_height*2/3));
		list[4] = new Vector2((float)(coords.x-hex_width/2),(float)(coords.y-hex_height/3));
		list[5] = new Vector2((float)(coords.x-hex_width/2),(float)(coords.y+hex_height/3));
		return list;
	}
	
	public Vector2[] RandomizeVerts(Vector2 [] coords, int num_verts, double hex_width, double hex_height, System.Random rand){
		double base_displacement = Math.Sqrt(Math.Pow(hex_width,2)+Math.Pow(hex_height,2))/3.5;
		double displacement;
		double x_dis;
		double y_dis;
		double angle;
		Vector2[] newcoords = new Vector2[num_verts];
		for (int i = 0; i < num_verts; i++){
			displacement = base_displacement * rand.NextDouble();
			angle = (rand.Next(360)/(2*Math.PI));
			x_dis = Math.Cos(angle) * displacement;
			y_dis = Math.Sin(angle) * displacement;
			newcoords[i] = new Vector2((float)(coords[i].x+x_dis),(float)(coords[i].y+y_dis));
		}
		return newcoords;
	}
	
	public double[,] Tectonic(int w, int h, System.Random rand){
		int num_points = 20 + rand.Next(3);
		int num_edges = 0;
		int edge_max = 4;
		int new_edge;
		int [] edges1 = new int[0];
		int [] edges2 = new int[0];
		PlateVector [] vectors = new PlateVector[num_points];
		//adds points randomly to plane
		for (int i = 0; i < num_points; i++){
			vectors[i] = new PlateVector(w,h,rand);
		}
		//gets rid of dupes
		for (int i = 0; i < num_points; i++){
			for (int j = i+1; j < num_points; j++){
				if (vectors[i].SamePoint(vectors[j])){
					vectors = DeleteVector(vectors,j,num_points);
					num_points--;
				}
			}
		}
		for (int i = 1; i < 2 /*edge_max + 1*/; i++){
			for (int j = 0; j < num_points; j++){
				if (vectors[j].EdgeCount() < 2 /*edge_max*/){
					new_edge = vectors[j].AddClosest(vectors,num_points,i,j);
					if (new_edge!=-1){
						edges1 = AddEdge(edges1,num_edges,new_edge);
						edges2 = AddEdge(edges2,num_edges,j);
						num_edges ++;
					}
				}
			}
		}
		for (int i = 0; i < num_points; i++){
			vectors[i].DebugVector();
		}
		
		//this is all just for visualisation
		double[,] map = new double[w,h];
		for (int i = 0; i < num_points; i++){
			map[vectors[i].x,vectors[i].y] = (double)4.0;
		}
		for (int i = 0; i < num_edges; i++){
			DrawEdge(map,vectors,edges1[i],edges2[i],rand);
		} //include once DrawPlate is working as intended
		//UnityEngine.Debug.Log("Vectors: " + num_points);
		//UnityEngine.Debug.Log("Edges: " + num_edges);
		return map;
	}
	
	
	
	private void DrawEdge(double[,] map, PlateVector[] vectors, int ind1, int ind2, System.Random rand){
		double distance = vectors[ind1].VectorDistance(vectors[ind2]);
		UnityEngine.Debug.Log("Distance: " + distance);
		double unit_x = (vectors[ind2].x-vectors[ind1].x)/distance;
		UnityEngine.Debug.Log("unit_x: " + unit_x);
		double unit_y = (vectors[ind2].y-vectors[ind1].y)/distance;
		int iter = 2;
		while(Math.Pow(2,iter) < distance) iter++;
		int itermax = (int)Math.Pow(2,iter)+1;
		double [] iterarray = new double[itermax];
		UnityEngine.Debug.Log("Variation Iterations: " + iter);
		int dist;
		double factor = 0.005;
		iterarray[0] = 0;
		iterarray[itermax-1] = 0;
		iter--;
		while(iter >= 0){
			dist = (int)Math.Pow(2,iter);
			for(int i = dist; i < itermax; i += 2*dist){
				iterarray[i] = (iterarray[i-dist] + iterarray[i+dist])/2 + factor*dist*rand.Next(-1,1)*rand.NextDouble();
			}
		}
		int x_ini = vectors[ind1].x;
		int y_ini = vectors[ind1].y;
		int x_add;
		int y_add;
		/*for (int i = 1; i < distance; i++){
			if ((double)unit_x*i%1 < 0.5) x_add = (int)Math.Floor(unit_x*i);
			else x_add = (int)Math.Ceiling(unit_x*i);
			if ((double)unit_y*i%1 < 0.5) y_add = (int)Math.Floor(unit_y*i);
			else y_add = (int)Math.Ceiling(unit_y*i);
			if (map[x_ini+x_add,y_ini+y_add]!=4.0) map[x_ini+x_add,y_ini+y_add]=2.0;
		}*/
		unit_x = (vectors[ind2].x-vectors[ind1].x)/itermax;
		unit_y = (vectors[ind2].y-vectors[ind1].y)/itermax;
		/*for (int i = 0; i < itermax; i++){
			if ((double)(unit_x+unit_y*iterarray[i])%1 < 0.5) x_add = (int)Math.Floor((double)(unit_x+unit_y*iterarray[i]));
			else x_add = (int)Math.Ceiling((double)(unit_x+unit_y*iterarray[i]));
			if ((double)(unit_y+unit_x*iterarray[i])%1 < 0.5) y_add = (int)Math.Floor((double)(unit_y+unit_x*iterarray[i]));
			else y_add = (int)Math.Ceiling((double)(unit_y+unit_x*iterarray[i]));
			if (map[x_ini+x_add,y_ini+y_add]!=4.0) map[x_ini+x_add,y_ini+y_add]=2.0;
		}*/
	}
	
	private PlateVector[] DeleteVector(PlateVector[] vectors, int ind, int num_points){
		PlateVector[] return_vectors = new PlateVector[num_points-1];
		for(int i = 0; i < ind; i++){
			return_vectors[i] = vectors[i];
		}
		for(int i = ind+1; i < num_points; i++){
			return_vectors[i-1] = vectors[i];
		}
		return return_vectors;
	}
	
	
	
	private int[] AddEdge(int[] old_edges, int num_edges, int v_ind){
		int [] new_edges = new int[num_edges+1];
		for (int i = 0; i < num_edges; i++){
			new_edges[i] = old_edges[i];
		}
		new_edges[num_edges] = v_ind;
		return new_edges;
	}
	
	public double[,] SmoothMap(double[,]newmap){
		double [,] dupemap = new double[width,height];
		double newvalue;
		int count;
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				dupemap[x,y] = newmap[x,y];
			}
		}
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				newvalue = 0;
				count = 0;
				for (int i = -2; i < 3; i++){
					for (int j = -2; j < 3; j++){
						//UnityEngine.Debug.Log(x+i);
						if ((x+i)>=0 && (x+i) < width && (y+j)>=0 && y+j < height){
							newvalue += dupemap[x+i,y+j];
							count++;
						}
					}
				}
				dupemap[x,y] = (newvalue)/count;
			}
		}
		return dupemap;
	}
}

public class Plate{
	private int[] edges;
	private double dir_x;
	private double dir_y;
}

public class PlateVector {
	private int[] edges;
	private int edge_count;
	public int x;
	public int y;
	public PlateVector(int w, int h, System.Random rand){
		x = rand.Next(w);
		y = rand.Next(h);
		edge_count = 0;
		edges = new int[0];
	}
	
	public bool SamePoint(PlateVector test){
		return (test.x == x && test.y == y);
	}
	
	public int EdgeCount(){
		return edge_count;
	}
	
	private void AddEdge(int ind){
		int[] newedges = new int[edge_count+1];
		edge_count += 1;
		for (int i = 0; i < edge_count-1; i++){
			newedges[i] = edges[i];
		}
		newedges[edge_count-1] = ind;
		edges = newedges;
	}
	
	private bool Connected(int ind){
		for (int i = 0; i < edge_count; i++){
			if (edges[i]==ind) return true;
		}
		return false;
	}
	
	public int AddClosest(PlateVector[] vectors, int num_points, int edge_max, int skip){
		int ind = 0;
		double min = 0;
		bool found = false;
		if (skip!=0) ind = 0;
		for (int i = 0; i < num_points; i++){
			if (i==skip){}
			else /*if (vectors[i].EdgeCount() < edge_max)*/{
				if (!found || (VectorDistance(vectors[i]) < min && !Connected(i))){
					if (ValidEdge(vectors, i)&&vectors[i].ValidEdge(vectors, skip)){
						ind = i;
						min = VectorDistance(vectors[ind]);
						found = true;
					}
				}
			}
		}
		if (found){
			AddEdge(ind);
			vectors[ind].AddEdge(skip);
			//UnityEngine.Debug.Log("Drew Edge Between " + x + ", " + y + " and " + vectors[ind].x + ", " +  vectors[ind].y);
			//UnityEngine.Debug.Log("Distance: " + min);
			//UnityEngine.Debug.Log("Edge Count: " + x + ", " + y + ": " + edge_count);
			//UnityEngine.Debug.Log("Edge Count: " + vectors[ind].x + ", " + vectors[ind].y + ": " + edge_count);
			return(ind);
		}
		else return -1;
	}
	public double VectorDistance(PlateVector vector2){
		return Math.Sqrt(Math.Pow((x-vector2.x),2)+Math.Pow((y-vector2.y),2));
	}
	
	public bool ValidEdge(PlateVector[] vectors, int ind){
		int x1 = vectors[ind].x - x;
		int y1 = vectors[ind].y - y;
		int x2;
		int y2;
		double angle;
		double min_angle = 50.0;
		for (int i = 0; i < edge_count; i++){
			x2 = vectors[i].x - x;
			y2 = vectors[i].y - y;
			angle = VectorAngle(x1, y1, x2, y2);
			if (Math.Abs(angle) < min_angle || Math.Abs(angle) > (360-min_angle)){
				return false;
			}
		}
		return true;
	}
	
	public void DebugVector(){
		//UnityEngine.Debug.Log("Vector " + x + ", " + y + ": Edges: " + edge_count);
	}
	
	private double VectorAngle(int x1, int y1, int x2, int y2){
		double sin = x1 * y2 - x2 * y1;  
		double cos = x1 * x2 + y1 * y2;

		return Math.Atan2(sin, cos) * (180 / Math.PI);
	}
}



public class PlateEdge {
	private PlateVector start;
	private PlateVector end;
}

public class HexPlate{
	private bool top;
	private bool bottom;
	private bool edge;
	private int[] vertices;
	private int[] edges;
	private Vector2 center;
	private Vector2 velocity;
	private bool ocean;
	public int opposite_borders;
	System.Random rnd;
	
	public HexPlate(System.Random rand){
		top = false;
		bottom = false;
		edge = false;
		vertices = new int[6];
		edges = new int[6];
		center = new Vector2();
		velocity = new Vector2();
		ocean = false;
		rnd = rand;
		opposite_borders = 0;
		
	}
	
	public void RandomizeDirection(){
		double angle = (rnd.Next(360)/(2*Math.PI));
		double magnitude = rnd.NextDouble();
		velocity.x = (float)(magnitude*Math.Cos(angle));
		velocity.y = (float)(magnitude*Math.Sin(angle));
	}
	
	public void SetOcean(bool yeah){
		ocean = yeah;
	}
	
	public void RandomizeOcean(){
		if (rnd.NextDouble() < 0.7) {
			ocean = false;
		}
		else {
			ocean = true;
		}
	}
	
	public bool GetOcean(){
		return ocean;
	}
	
	public Vector2 GetDirection(){
		return velocity;
	}
	
	public void SetCenter(Vector2 vector){
		center.x = vector.x;
		center.y = vector.y;
	}
	
	public Vector2 GetCenter(){
		return center;
	}
	
	public int[] GetVertices(){
		return vertices;
	}
	
	public void SetVertices(int [] list){
		for (int i = 0; i < 6; i++){
			vertices[i] = list[i];
		}
	}
	
	public int[] GetEdges(){
		return edges;
	}
	
	public void SetEdges(int [] list){
		for (int i = 0; i < 6; i++){
			edges[i] = list[i];
		}
	}
}

public class Edge {
	public Vector2 vector1;
	public Vector2 vector2;
	private System.Random rnd;
	private int[] plates;
	public int num_plates;
	double weight;
	double[] displacement;
	//private HexPlate plate1;
	//private HexPlate plate2;
	
	public bool Equals(Edge other){
		if ((Math.Abs(other.vector1.x-vector1.x) < 0.4
			&& Math.Abs(other.vector1.y-vector1.y) < 0.4
			&& Math.Abs(other.vector2.x-vector2.x) < 0.4
			&& Math.Abs(other.vector2.y-vector2.y) < 0.4)
			||
			(Math.Abs(other.vector2.x-vector1.x) < 0.4
			&& Math.Abs(other.vector2.y-vector1.y) < 0.4
			&& Math.Abs(other.vector1.x-vector2.x) < 0.4
			&& Math.Abs(other.vector1.y-vector2.y) < 0.4))
			return true;
		else return false;
	}
	
	public void SetWeight(double newweight){
		weight = newweight;
	}
	
	public void SetDisplacement(double[] newlist){
		displacement = newlist;
	}
	
	public void AddPlate(int newind){
		if (num_plates==0){
			plates[0] = newind;
		}
		else plates[1] = newind;
		num_plates++;
	}
	
	public int [] GetPlates(){
		return plates;
	}
	
	public void DrawEdge(double [,] map, int w, int h){
		double distance = Math.Sqrt(Math.Pow((vector2.x-vector1.x),2)+Math.Pow((vector2.y-vector1.y),2));
		double unit_x = (vector2.x-vector1.x)/distance;
		double unit_y = (vector2.y-vector1.y)/distance;
		int x_ini = (int)vector1.x;
		int y_ini = (int)vector1.y;
		double x_new;
		double y_new;
		double h_add;
		double h_temp;
		int x_add;
		int y_add;
		for (int i = 1; i < distance*2; i++){
			h_add = rnd.NextDouble()*weight;
			x_new = unit_x*i/2 + unit_y*displacement[(int)((i)/(distance)*16)];
			y_new = unit_y*i/2 - unit_x*displacement[(int)((i)/(distance)*16)];
			if ((double)x_new%1 < 0.5) x_add = (int)Math.Floor(x_new);
			else x_add = (int)Math.Ceiling(x_new);
			if ((double)y_new%1 < 0.5) y_add = (int)Math.Floor(y_new);
			else y_add = (int)Math.Ceiling(y_new);
			for (int x = -4; x <= 4; x++){
				for (int y = -4; y <=4; y++){
					h_temp = Math.Cos(x/2.6)*Math.Cos(y/2.6)*h_add;
					if (x_ini+x_add+x >= 0 && x_ini + x_add +x < w && y_ini+y_add+y >= 0 && y_ini+y_add+y < h){
						/*if (map[x_ini+x_add,y_ini+y_add]!=4.0)*/
						map[x_ini+x_add+x,y_ini+y_add+y]+=h_temp;
						//map[x_ini+x_add,y_ini+y_add]=2.0;
					}
				}
			}
			//if (x_ini+x_add >= 0 && x_ini + x_add < w && y_ini+y_add >= 0 && y_ini+y_add < h){
				//if (map[x_ini+x_add,y_ini+y_add]!=4.0)
				//map[x_ini+x_add,y_ini+y_add]+=rnd.NextDouble()*weight;
				//map[x_ini+x_add,y_ini+y_add]=2.0;
			//}
		}
	}
	
	public void SetVectors(Vector2 new1, Vector2 new2, System.Random rand){
		vector1 = new1;
		vector2 = new2;
		plates = new int[2];
		num_plates = 0;
		rnd = rand;
	}
}
	
public class MapGenerator : MonoBehaviour {
	int map_width, map_height, island_size;
	Stopwatch timer = new Stopwatch();
	Map map;
	System.Random rnd = new System.Random();
	// Use this for initialization
	void Start () {
		map_width = 180;
		map_height = 120;
		map = new Map(map_width,map_height,rnd);
		timer.Reset();
		timer.Start();
		map.GenerateLandMass();
		UnityEngine.Debug.Log("Time to generate map: " + timer.ElapsedMilliseconds);
		map.AddRiverSources(1);
		map.WaterPass();
		map.MoistureCalc();
		map.SetBiomes();
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown(KeyCode.Return)){
			//map_width = 200 + rnd.Next(21);
			//map_height = 120 + rnd.Next(21);
			//map = new Map(map_width,map_height,rnd);
			timer.Reset();
			timer.Start();
			map.GenerateLandMass();
			UnityEngine.Debug.Log("Time to generate map: " + timer.ElapsedMilliseconds);
			map.AddRiverSources(1);
			map.WaterPass();
			map.MoistureCalc();
			map.SetBiomes();
		}
	
	}
	
	int TileBiome(int x, int y){
		return map.TileBiome(x,y);
	}
}
