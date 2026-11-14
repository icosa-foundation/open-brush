"""Push up the middle of a straight stroke to create a local arch."""

from automation_sections import reshape_demo


def build_plan(args):
    points = [[-0.13 + 0.46 * i / 96, -0.23, 0.45] for i in range(97)]
    return reshape_demo("Push", points, [[0.10, -0.275, 0.45], [0.10, -0.155, 0.45]],
                        name="Push a straight stroke into an arch", radius=0.075,
                        seconds=2.8, minimum_movement=0.05, fixed_endpoints=True,
                        brush=args.brush or "Icing")
