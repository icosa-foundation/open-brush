Settings = {
    description="Draws a cube",
    previewType="cube"
}

function OnTriggerReleased()

    paths = MultiPath:New()
    face = Path:New()

    -- front face
    face:Insert(Transform:New(Vector3:New(-1, 1, 1), Rotation.zero)) -- top left
    face:Insert(Transform:New(Vector3:New(1, 1, 1), Rotation.zero)) -- top right
    face:Insert(Transform:New(Vector3:New(1, -1, 1), Rotation.zero)) -- bottom right
    face:Insert(Transform:New(Vector3:New(-1, -1, 1), Rotation.zero)) -- bottom left
    face:Insert(Transform:New(Vector3:New(-1, 1, 1), Rotation.zero)) -- top left (to close the loop)
    paths:Insert(face)

    -- right face
    face:Insert(Transform:New(Vector3:New(1, 1, 1), Rotation:New(0, 0, 90))) -- top right
    face:Insert(Transform:New(Vector3:New(1, 1, -1), Rotation:New(0, 0, 90))) -- top back
    face:Insert(Transform:New(Vector3:New(1, -1, -1), Rotation:New(0, 0, 90))) -- bottom back
    face:Insert(Transform:New(Vector3:New(1, -1, 1), Rotation:New(0, 0, 90))) -- bottom right
    face:Insert(Transform:New(Vector3:New(1, 1, 1), Rotation:New(0, 0, 90))) -- top right (to close the loop)
    paths:Insert(face)

    -- back face
    face:Insert(Transform:New(Vector3:New(1, 1, -1), Rotation:New(0, 0, 180))) -- top back
    face:Insert(Transform:New(Vector3:New(-1, 1, -1), Rotation:New(0, 0, 180))) -- top left
    face:Insert(Transform:New(Vector3:New(-1, -1, -1), Rotation:New(0, 0, 180))) -- bottom left
    face:Insert(Transform:New(Vector3:New(1, -1, -1), Rotation:New(0, 0, 180))) -- bottom back
    face:Insert(Transform:New(Vector3:New(1, 1, -1), Rotation:New(0, 0, 180))) -- top back (to close the loop)
    paths:Insert(face)

    -- left face
    face:Insert(Transform:New(Vector3:New(-1, 1, -1), Rotation:New(0, 0, 270))) -- top left
    face:Insert(Transform:New(Vector3:New(-1, 1, 1), Rotation:New(0, 0, 270))) -- top front
    face:Insert(Transform:New(Vector3:New(-1, -1, 1), Rotation:New(0, 0, 270))) -- bottom front
    face:Insert(Transform:New(Vector3:New(-1, -1, -1), Rotation:New(0, 0, 270))) -- bottom left
    face:Insert(Transform:New(Vector3:New(-1, 1, -1), Rotation:New(0, 0, 270))) -- top left (to close the loop)
    paths:Insert(face)

    return paths
end
