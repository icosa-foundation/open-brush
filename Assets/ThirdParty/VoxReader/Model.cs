using VoxReader.Interfaces;

namespace VoxReader
{
    public class Model : IModel
    {
        public string Name { get; }
        public Vector3 GlobalPosition { get; }
        public Vector3 LocalPosition { get; }
        public Matrix3 GlobalRotation { get; }
        public Matrix3 LocalRotation { get; }
        public Vector3 GlobalSize => (GlobalRotation * LocalSize).ToAbsolute();
        public Vector3 LocalSize { get; }
        public Voxel[] Voxels { get; }
        public int Id { get; }
        public bool IsCopy { get; }

        public Model(int id, string name, Voxel[] voxels, bool isCopy, Vector3 position, Vector3 localPosition, Matrix3 rotation, Matrix3 localRotation, Vector3 localSize)
        {
            Id = id;
            Name = name;
            Voxels = voxels;
            IsCopy = isCopy;

            GlobalPosition = position;
            LocalPosition = localPosition;

            GlobalRotation = rotation;
            LocalRotation = localRotation;

            LocalSize = localSize;
        }

        internal Model GetCopy()
        {
            return new(Id, Name, Voxels, true, GlobalPosition, LocalPosition, GlobalRotation, LocalRotation, LocalSize);
        }

        public override string ToString()
        {
            return $"Id: {Id}, Name: {Name}, Global Position: {GlobalPosition}, Local Position: {LocalPosition}, Global Rotation: {GlobalRotation}, Local Rotation: {LocalRotation}, Global Size: {GlobalSize}, Local Size: {LocalSize}, Voxel Count: {Voxels.Length}";
        }
    }
}