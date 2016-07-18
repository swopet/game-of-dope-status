var width;
var height;
var map;
var textures;
var scrollPosition = Vector2.zero;
function Start () {
	textures = new Texture[8];
	textures[0] = Resources.Load("coast") as Texture;
	textures[1] = Resources.Load("desert") as Texture;
	textures[2] = Resources.Load("forest") as Texture;
	textures[3] = Resources.Load("grass") as Texture;
	textures[4] = Resources.Load("mountain") as Texture;
	textures[5] = Resources.Load("ocean") as Texture;
	textures[6] = Resources.Load("river") as Texture;
	Debug.Log("Whoo");
	width = GetComponent("MapGenerator").map_width;
	height = GetComponent("MapGenerator").map_height;
	map = new Array(width);
	for (var i = 0; i < width; i++){
		map[i] = new Array(height);
	}
	var biome;
	for (var x = 0; x < width; x++){
		for (var y = 0; y < height; y++){
			map[x][y] = GetComponent("MapGenerator").TileBiome(x,y);
			//Debug.Log(x + ", " + y + ": " + map[x][y]);
		}
	}
}

function OnGUI(){
	scrollPosition = GUI.BeginScrollView(new Rect(0,0,Screen.width,Screen.height),scrollPosition,new Rect(0,0,4*width,4*height));
	for (var x = 0; x < width; x++){
		for (var y = 0; y < height; y++){
			GUI.DrawTexture(new Rect(4*x,4*y,4,4), textures[map[x][y]]);
		}
	}
	GUI.EndScrollView();
	//GUI.DrawTexture(new Rect(0,0,16,16), textures[0]);
}

function Update () {
	if (Input.GetKeyDown(KeyCode.Return)){
		width = GetComponent("MapGenerator").map_width;
		height = GetComponent("MapGenerator").map_height;
		for (var x = 0; x < width; x++){
			for (var y = 0; y < height; y++){
				map[x][y] = GetComponent("MapGenerator").TileBiome(x,y);
				//Debug.Log(x + ", " + y + ": " + map[x][y]);
			}
		}
	}
}	