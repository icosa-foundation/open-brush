namespace VoxReader.Interfaces
{
    public interface IModel
    {
        /// <summary>
        /// The name of the model.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The global position of the model in the world.
        /// </summary>
        Vector3 GlobalPosition { get; }

        /// <summary>
        /// The position of the model relative to its parent.
        /// </summary>
        Vector3 LocalPosition { get; }

        /// <summary>
        /// The global rotation of the model in the world. Includes rotations of all parents.
        /// </summary>
        Matrix3 GlobalRotation { get; }

        /// <summary>
        /// The rotation of the model relative to its parent.
        /// </summary>
        Matrix3 LocalRotation { get; }

        /// <summary>
        /// The global size of the model. MagicaVoxel models don't have a scale factor, but rotations can swap side lengths.
        /// <see cref="LocalSize"/> and <see cref="GlobalSize"/> will always have the same volume, but may be differently rotated from each other. 
        /// </summary>
        Vector3 GlobalSize { get; }


        /// <summary>
        /// The local size of the model. MagicaVoxel models don't have a scale factor, but rotations can swap side lengths.
        /// <see cref="LocalSize"/> and <see cref="GlobalSize"/> will always have the same volume, but may be differently rotated from each other. 
        /// </summary>
        Vector3 LocalSize { get; }

        /// <summary>
        /// All voxels that belong to the model.
        /// </summary>
        Voxel[] Voxels { get; }
        
        /// <summary>
        /// The id of the model.
        /// </summary>
        /// <remarks>Copies of models share the same id.</remarks>
        int Id { get; }
        
        /// <summary>
        /// Indicates if the model is a copy another model.
        /// </summary>
        bool IsCopy { get; }
    }
}