"""
Pelatihan model RandomForestClassifier untuk klasifikasi perilaku evakuasi
(Bagian 2 & 3 spesifikasi: membaca evacuation_dataset.csv, melatih model,
mencetak metrik evaluasi, dan menyimpan behavior_model.pkl).
"""

import argparse
from pathlib import Path
import joblib
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix
from sklearn.model_selection import train_test_split

FEATURE_COLUMNS = ["v", "omega", "C", "R"]
TARGET_COLUMN = "label"
STATE_NAMES = ["Tenang", "Ragu-Ragu", "Panik"]


def train_model(
    data_path: Path,
    model_path: Path,
    test_size: float = 0.2,
    random_state: int = 42,
) -> None:
    """Membaca dataset CSV, melatih RandomForestClassifier, dan menyimpan model."""
    if not data_path.exists():
        raise FileNotFoundError(f"Dataset tidak ditemukan: {data_path}")

    df = pd.read_csv(data_path)
    required_cols = set(FEATURE_COLUMNS + [TARGET_COLUMN])
    missing = required_cols - set(df.columns)
    if missing:
        raise ValueError(f"Kolom berikut tidak ada di CSV: {sorted(missing)}")

    X = df[FEATURE_COLUMNS].to_numpy(dtype=float)
    y = df[TARGET_COLUMN].to_numpy(dtype=int)

    X_train, X_test, y_train, y_test = train_test_split(
        X,
        y,
        test_size=test_size,
        random_state=random_state,
        stratify=y,
    )

    clf = RandomForestClassifier(
        n_estimators=100,
        random_state=random_state,
    )
    clf.fit(X_train, y_train)

    y_pred = clf.predict(X_test)
    acc = accuracy_score(y_test, y_pred)

    print(f"Jumlah sampel latih : {len(X_train)}")
    print(f"Jumlah sampel uji   : {len(X_test)}")
    print(f"Akurasi             : {acc:.4f}")
    print("\nClassification Report:")
    print(
        classification_report(
            y_test,
            y_pred,
            labels=[0, 1, 2],
            target_names=STATE_NAMES,
            zero_division=0,
        )
    )
    print("Confusion Matrix (baris=aktual, kolom=prediksi):")
    print(confusion_matrix(y_test, y_pred, labels=[0, 1, 2]))

    model_path.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(clf, model_path)
    print(f"\nModel berhasil disimpan ke: {model_path}")


def main() -> None:
    base_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(
        description="Latih RandomForestClassifier dari evacuation_dataset.csv."
    )
    parser.add_argument(
        "--data",
        type=Path,
        default=base_dir / "data" / "evacuation_dataset.csv",
        help="Path file CSV dataset.",
    )
    parser.add_argument(
        "--model",
        type=Path,
        default=base_dir / "model" / "behavior_model.pkl",
        help="Path keluaran file model .pkl.",
    )
    parser.add_argument(
        "--test-size",
        type=float,
        default=0.2,
        help="Proporsi data uji (default 0.2).",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=42,
        help="Random seed.",
    )
    args = parser.parse_args()

    train_model(args.data, args.model, args.test_size, args.seed)


if __name__ == "__main__":
    main()

