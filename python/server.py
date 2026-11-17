"""
Server inferensi UDP real-time untuk klasifikasi perilaku evakuasi
(Bagian 1 & 2 spesifikasi: menerima JSON fitur pada port 5005, melakukan
inferensi RandomForestClassifier dengan max(predict_proba), menerapkan
histeresis, dan membalas JSON ke port 5006).
"""

import argparse
import json
import socket
from pathlib import Path
import joblib
import numpy as np

from hysteresis import HysteresisFilter


def run_server(
    host: str,
    recv_port: int,
    send_port: int,
    model_path: Path,
    verbose: bool = True,
) -> None:
    """Menjalankan loop server UDP inferensi."""
    if not model_path.exists():
        raise FileNotFoundError(
            f"Model tidak ditemukan di {model_path}. Jalankan train.py terlebih dahulu."
        )

    model = joblib.load(model_path)
    hysteresis = HysteresisFilter()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((host, recv_port))
    sock.settimeout(1.0)

    print(f"[Server] Model dimuat: {model_path}")
    print(f"[Server] Mendengarkan UDP di {host}:{recv_port}, membalas ke port {send_port}")
    print("[Server] Tekan Ctrl+C untuk berhenti.")

    try:
        while True:
            try:
                data, addr = sock.recvfrom(4096)
            except socket.timeout:
                continue

            try:
                payload = json.loads(data.decode("utf-8"))
                features = payload.get("features")
                if not isinstance(features, list) or len(features) != 4:
                    if verbose:
                        print(f"[Server] Format fitur tidak valid: {payload}")
                    continue

                v, omega, c_count, r_count = [float(x) for x in features]
                X = np.array([[v, omega, c_count, r_count]], dtype=float)

                proba = model.predict_proba(X)[0]
                best_idx = int(np.argmax(proba))
                predicted_label = int(model.classes_[best_idx])
                confidence = float(np.max(proba))

                official_state, final_conf = hysteresis.update(
                    predicted_label=predicted_label,
                    confidence=confidence,
                    v=v,
                    omega=omega,
                )

                response = {
                    "state": official_state,
                    "confidence": round(final_conf, 4),
                }
                sock.sendto(
                    json.dumps(response).encode("utf-8"),
                    (addr[0], send_port),
                )

                if verbose:
                    print(
                        f"[Inferensi] in=[v={v:.2f}, omega={omega:.1f}, C={c_count:.0f}, R={r_count:.0f}] "
                        f"-> raw={predicted_label} ({confidence:.2f}) | resmi={official_state}"
                    )
            except (json.JSONDecodeError, TypeError, ValueError) as exc:
                if verbose:
                    print(f"[Server] Gagal memproses paket: {exc}")
    except KeyboardInterrupt:
        print("\n[Server] Dihentikan oleh pengguna.")
    finally:
        sock.close()


def main() -> None:
    base_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(
        description="Server UDP inferensi perilaku evakuasi."
    )
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Alamat IP lokal.")
    parser.add_argument("--recv-port", type=int, default=5005, help="Port UDP masuk.")
    parser.add_argument("--send-port", type=int, default=5006, help="Port UDP balasan.")
    parser.add_argument(
        "--model",
        type=Path,
        default=base_dir / "model" / "behavior_model.pkl",
        help="Path file model .pkl.",
    )
    parser.add_argument("--quiet", action="store_true", help="Matikan log per paket.")
    args = parser.parse_args()

    run_server(
        host=args.host,
        recv_port=args.recv_port,
        send_port=args.send_port,
        model_path=args.model,
        verbose=not args.quiet,
    )


if __name__ == "__main__":
    main()

