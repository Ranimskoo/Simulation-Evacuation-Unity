"""
PERINGATAN PENTING:
Skrip ini menghasilkan data sintetis HANYA untuk menguji pipeline pelatihan dan
inferensi model (Bagian 3 spesifikasi). Data final untuk penelitian dan
evaluasi model WAJIB dikumpulkan dari sesi playtest asli menggunakan
DataCollector.

Rentang profil per window (0,5 detik):
- Label 0 (Tenang)    : omega < 45; v 1.4-2.0; C = 0; R 0-1
- Label 1 (Ragu-Ragu) : omega 45-100; v < 0.5; C 0-1; R 1-2
- Label 2 (Panik)     : omega > 120; v > 2.2; C >= 2; R >= 3
"""

import argparse
from pathlib import Path
import numpy as np
import pandas as pd


def generate_seed_dataset(samples_per_class: int = 600, seed: int = 42) -> pd.DataFrame:
    """
    Membuat dataset sintetis seimbang (~1800 baris default) dengan kombinasi multi-faktor
    (v, omega, C, R):
    - 0 (Tenang)    : berjalan/diam stabil tanpa jitter atau benturan beruntun (termasuk
                      1 benturan ringan tunggal saat berjalan normal & tenang).
    - 1 (Ragu-Ragu) : faktor mulai terpenuhi secara parsial (ragu/berhenti sambil menoleh,
                      hanya lari tanpa jitter/benturan, hanya menabrak obstacle tanpa lari,
                      atau hanya menggeser kamera cepat tanpa lari).
    - 2 (Panik)     : gabungan multi-faktor aktif bersamaan (lari v > 2.2 + mouse jitter
                      tinggi omega > 120 + benturan beruntun / pembalikan arah).
    """
    rng = np.random.default_rng(seed)
    rows = []

    n0_walk = max(1, int(samples_per_class * 0.50))
    n0_idle = max(1, int(samples_per_class * 0.25))
    n0_bump = max(1, samples_per_class - n0_walk - n0_idle)

    # Kelas 0: Tenang
    for _ in range(n0_walk):
        v = rng.uniform(0.9, 2.05)
        omega = rng.uniform(1.0, 44.0)
        c = 0
        r = int(rng.integers(0, 2))
        rows.append((round(v, 4), round(omega, 4), c, r, 0))

    for _ in range(n0_idle):
        v = rng.uniform(0.0, 0.85)
        omega = rng.uniform(0.0, 42.0)
        rows.append((round(v, 4), round(omega, 4), 0, 0, 0))

    for _ in range(n0_bump):
        # Hanya menabrak 1x secara tidak sengaja saat berjalan/pelan & pandangan stabil -> tetap Tenang
        v = rng.uniform(0.0, 1.95)
        omega = rng.uniform(0.0, 42.0)
        rows.append((round(v, 4), round(omega, 4), 1, 0, 0))

    n1_part = max(1, samples_per_class // 5)
    n1_last = max(1, samples_per_class - 4 * n1_part)

    # Kelas 1: Ragu-Ragu (faktor mulai terpenuhi / parsial, belum kombinasi panik penuh)
    for _ in range(n1_part):
        # 1a: Ragu klasik (diam/pelan sambil menoleh cepat & bolak-balik arah)
        v = rng.uniform(0.0, 0.85)
        omega = rng.uniform(45.0, 118.0)
        c = int(rng.integers(0, 2))
        r = int(rng.integers(1, 3))
        rows.append((round(v, 4), round(omega, 4), c, r, 1))

    for _ in range(n1_part):
        # 1b: Baru mulai mempercepat langkah ringan tanpa faktor lain
        v = rng.uniform(2.06, 2.34)
        omega = rng.uniform(2.0, 43.0)
        c = 0
        r = int(rng.integers(0, 2))
        rows.append((round(v, 4), round(omega, 4), c, r, 1))

    for _ in range(n1_part):
        # 1c: Menggeser mouse cukup cepat tanpa berlari & tanpa benturan beruntun
        v = rng.uniform(0.0, 2.02)
        omega = rng.uniform(45.0, 120.0)
        c = int(rng.integers(0, 2))
        r = int(rng.integers(0, 3))
        rows.append((round(v, 4), round(omega, 4), c, r, 1))

    for _ in range(n1_part):
        # 1d: Menabrak obstacle beruntun TETAPI tidak berlari dan tidak jitter ekstrem -> belum Panik
        v = rng.uniform(0.0, 2.02)
        omega = rng.uniform(2.0, 115.0)
        c = int(rng.integers(2, 6))
        r = int(rng.integers(0, 3))
        rows.append((round(v, 4), round(omega, 4), c, r, 1))

    for _ in range(n1_last):
        # 1e: Bolak-balik arah / bingung rute saat berjalan normal
        v = rng.uniform(0.2, 2.02)
        omega = rng.uniform(10.0, 115.0)
        c = int(rng.integers(0, 2))
        r = int(rng.integers(2, 5))
        rows.append((round(v, 4), round(omega, 4), c, r, 1))

    n2_full = max(1, int(samples_per_class * 0.30))
    n2_sprint = max(1, int(samples_per_class * 0.25))
    n2_jitter = max(1, int(samples_per_class * 0.25))
    n2_combo = max(1, samples_per_class - n2_full - n2_sprint - n2_jitter)

    # Kelas 2: Panik (kombinasi multi-faktor atau lari sprint / kamera liar terus-menerus)
    for _ in range(n2_full):
        # 2a: Profil Panik penuh sesuai spesifikasi
        v = rng.uniform(2.25, 4.2)
        omega = rng.uniform(122.0, 300.0)
        c = int(rng.integers(2, 7))
        r = int(rng.integers(3, 8))
        rows.append((round(v, 4), round(omega, 4), c, r, 2))

    for _ in range(n2_sprint):
        # 2b: Berlari sprint penuh terus-menerus (v >= 2.38 m/s), termasuk saat C=0 & R=0
        v = rng.uniform(2.38, 3.8)
        omega = rng.uniform(0.0, 120.0)
        c = int(rng.choice([0, 0, 0, 1, 2]))
        r = int(rng.choice([0, 0, 0, 1, 2]))
        rows.append((round(v, 4), round(omega, 4), c, r, 2))

    for _ in range(n2_jitter):
        # 2c: Menggeser mouse sangat cepat secara terus-menerus (omega > 122 deg/s), termasuk saat C=0 & R=0
        v = rng.uniform(0.0, 3.6)
        omega = rng.uniform(122.0, 300.0)
        c = int(rng.choice([0, 0, 0, 1, 2]))
        r = int(rng.choice([0, 0, 0, 1, 2]))
        rows.append((round(v, 4), round(omega, 4), c, r, 2))

    for _ in range(n2_combo):
        # 2d: Kombinasi lari sedang-cepat + menabrak obstacle atau kamera gelisah / bolak-balik
        v = rng.uniform(2.12, 3.5)
        omega = rng.uniform(45.0, 240.0)
        c = int(rng.integers(1, 5))
        r = int(rng.integers(1, 5))
        rows.append((round(v, 4), round(omega, 4), c, r, 2))

    df = pd.DataFrame(rows, columns=["v", "omega", "C", "R", "label"])
    df = df.sample(frac=1.0, random_state=seed).reset_index(drop=True)
    return df


def main() -> None:
    base_dir = Path(__file__).resolve().parent
    default_output = base_dir / "data" / "evacuation_dataset.csv"

    parser = argparse.ArgumentParser(
        description="Generator dataset sintetis sementara untuk pengujian pipeline."
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=default_output,
        help="Path file CSV keluaran.",
    )
    parser.add_argument(
        "--samples-per-class",
        type=int,
        default=600,
        help="Jumlah sampel per kelas (default 600 -> total 1800 baris).",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=42,
        help="Random seed untuk reprodusibilitas.",
    )
    args = parser.parse_args()

    args.output.parent.mkdir(parents=True, exist_ok=True)
    df = generate_seed_dataset(args.samples_per_class, args.seed)
    df.to_csv(args.output, index=False)
    print(f"Dataset sintetis ({len(df)} baris) disimpan ke: {args.output}")
    print("Distribusi label:")
    print(df["label"].value_counts().sort_index())


if __name__ == "__main__":
    main()

