Settings = {
    description = "Draws the wireframe of a chosen Platonic solid",
    previewType = "cube"
}

Parameters = {
    solidType = {
        label = "Solid Type",
        type = "list",
        items= {"Tetrahedron", "Cube", "Octahedron", "Dodecahedron", "Icosahedron"},
        default = "Icosahedron"
    },
    spacing = {label="Point Spacing", type="float", min=0.1, max=1, default=0.1}
}

-- Helper function to get vertices and faces of Platonic solids
function GetPlatonicSolidData(solidType)
    local vertices = {}
    local faces = {}

    if solidType == "Tetrahedron" then
        vertices = {
            Vector3:New(1, 1, 1), Vector3:New(-1, -1, 1),
            Vector3:New(-1, 1, -1), Vector3:New(1, -1, -1)
        }
        faces = {
            {1, 2, 3}, {1, 2, 4}, {1, 3, 4}, {2, 3, 4}
        }
    elseif solidType == "Cube" then
        vertices = {
            Vector3:New(-1, -1, -1), Vector3:New(1, -1, -1),
            Vector3:New(1, 1, -1), Vector3:New(-1, 1, -1),
            Vector3:New(-1, -1, 1), Vector3:New(1, -1, 1),
            Vector3:New(1, 1, 1), Vector3:New(-1, 1, 1)
        }
        faces = {
            {1, 2, 3, 4}, {5, 6, 7, 8}, {1, 2, 6, 5},
            {2, 3, 7, 6}, {3, 4, 8, 7}, {4, 1, 5, 8}
        }
    elseif solidType == "Octahedron" then

        vertices = {
            Vector3:New(0, 0, 1), Vector3:New(0, 0, -1),
            Vector3:New(1, 0, 0), Vector3:New(-1, 0, 0),
            Vector3:New(0, 1, 0), Vector3:New(0, -1, 0)
        }
        faces = {
            {1, 3, 5}, {1, 5, 4}, {1, 4, 6}, {1, 6, 3},
            {2, 3, 5}, {2, 5, 4}, {2, 4, 6}, {2, 6, 3}
        }

    elseif solidType == "Dodecahedron" then

        local root5 = Math:Sqrt(5);
        local phi = (1 + root5) / 2;
		local phibar = (1 - root5) / 2;
        local X = 1/(root5-1);
        local Y = X*phi;
        local Z = X*phibar;
        local S = -X;
        local T = -Y;
        local W = -Z;

        vertices = {
            Vector3:New(X, X, X),
            Vector3:New(X, X, S),
            Vector3:New(X, S, X),
            Vector3:New(X, S, S),
            Vector3:New(S, X, X),
            Vector3:New(S, X, S),
            Vector3:New(S, S, X),
            Vector3:New(S, S, S),
            Vector3:New(W, Y, 0),
            Vector3:New(Z, Y, 0),
            Vector3:New(W, T, 0),
            Vector3:New(Z, T, 0),
            Vector3:New(Y, 0, W),
            Vector3:New(Y, 0, Z),
            Vector3:New(T, 0, W),
            Vector3:New(T, 0, Z),
            Vector3:New(0, W, Y),
            Vector3:New(0, Z, Y),
            Vector3:New(0, W, T),
            Vector3:New(0, Z, T),
        }

        faces = {
            {2, 9, 1, 13, 14},
            {5, 10, 6, 16, 15},
            {3, 11, 4, 14, 13},
            {8, 12, 7, 15, 16},
            {3, 13, 1, 17, 18},
            {2, 14, 4, 20, 19},
            {5, 15, 7, 18, 17},
            {8, 16, 6, 19, 20},
            {5, 17, 1, 9, 10},
            {3, 18, 7, 12, 11},
            {2, 19, 6, 10, 9},
            {8, 20, 4, 11, 12}
        }

    elseif solidType == "Icosahedron" then

            local root5 = Math:Sqrt(5);
            local n = 1/2;
            local X = n * (1 + root5) / 2;
            local Y = -X;
            local Z = n;
            local W = -n;

			vertices = {
                Vector3:New(X, Z, 0),
                Vector3:New(Y, Z, 0),
                Vector3:New(X, W, 0),
                Vector3:New(Y, W, 0),
                Vector3:New(Z, 0, X),
                Vector3:New(Z, 0, Y),
                Vector3:New(W, 0, X),
                Vector3:New(W, 0, Y),
                Vector3:New(0, X, Z),
                Vector3:New(0, Y, Z),
                Vector3:New(0, X, W),
                Vector3:New(0, Y, W),
            }

            faces = {
                {1, 9, 5},
                {1, 6, 11},
                {3, 5, 10},
                {3, 12, 6},
                {2, 7, 9},
                {2, 11, 8},
                {4, 10, 7},
                {4, 8, 12},
                {1, 11, 9},
                {2, 9, 11},
                {3, 10, 12},
                {4, 12, 10},
                {5, 3, 1},
                {6, 1, 3},
                {7, 2, 4},
                {8, 4, 2},
                {9, 7, 5},
                {10, 5, 7},
                {11, 6, 8},
                {12, 8, 6}
            }

        end

    return vertices, faces
end

function Main()
    if Brush.triggerReleasedThisFrame then
        local vertices, faces = GetPlatonicSolidData(Parameters.solidType)
        local pathList = PathList:New()

        for _, face in ipairs(faces) do
            local path = Path:New()
            for _, vertexIndex in ipairs(face) do
                path:Insert(Transform:New(vertices[vertexIndex]))
            end
            path:Insert(Transform:New(vertices[face[1]])) -- Close the loop
            path:SampleByDistance(Parameters.spacing) -- Create evenly spaced points
            pathList:Insert(path)
        end

        return pathList
    end
end
