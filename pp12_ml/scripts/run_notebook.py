from pathlib import Path
import argparse

import nbformat
from nbclient import NotebookClient


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Execute PP12 classifier notebook.")
    parser.add_argument(
        "--save-executed",
        action="store_true",
        help="Save executed copy to notebooks/pp12_task_classifier_executed.ipynb.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    project_root = Path(__file__).resolve().parents[1]
    notebook_path = project_root / "notebooks" / "pp12_task_classifier.ipynb"
    executed_path = project_root / "notebooks" / "pp12_task_classifier_executed.ipynb"

    notebook = nbformat.read(notebook_path, as_version=4)
    client = NotebookClient(
        notebook,
        timeout=600,
        kernel_name="python3",
        resources={"metadata": {"path": str(project_root)}},
    )
    client.execute()

    if args.save_executed:
        nbformat.write(notebook, executed_path)
        print(f"Executed notebook saved to {executed_path}")

    print("Notebook executed successfully.")
    print(f"Artifacts directory: {project_root / 'artifacts'}")


if __name__ == "__main__":
    main()
