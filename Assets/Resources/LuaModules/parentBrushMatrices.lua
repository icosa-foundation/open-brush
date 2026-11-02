-- Convert absolute canvas pointer poses into MatrixList actions. Canvas Paths
-- retain their older translation-offset meaning.
local matrices = {}

local function poseMatrix(pose)
    local scale = Vector3:New(pose.scale, pose.scale, pose.scale)
    return Matrix:NewTRS(pose.position, pose.rotation, scale)
end

function matrices.fromPoses(poses, basePose)
    local result = MatrixList:New(poses.count)
    local baseInverse = poseMatrix(basePose).inverse
    for i = 0, poses.count - 1 do
        result[i] = poseMatrix(poses[i]) * baseInverse
    end
    return result
end

return matrices
