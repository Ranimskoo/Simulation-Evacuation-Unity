"""
Filter histeresis untuk menstabilkan keluaran status perilaku sebelum dikirim
ke klien penerima (Bagian 2 spesifikasi: Lapis Python).
"""

from dataclasses import dataclass
from typing import Optional, Tuple

LABEL_TO_STATE = {
    0: "Tenang",
    1: "Ragu-Ragu",
    2: "Panik",
}


@dataclass
class HysteresisConfig:
    """Parameter konfigurasi histeresis."""

    confidence_threshold: float = 0.80  # Batas bertindak >= 0,80
    stability_windows: int = 3          # Minimal 3 window berturut-turut (1,5 detik)
    window_duration: float = 0.5        # Durasi 1 sliding window (detik)
    calm_recovery_duration: float = 2.0 # Koreksi salah deteksi setelah 2,0 detik diam & stabil
    calm_max_speed: float = 0.5         # v < 0,5 m/detik
    calm_max_jitter: float = 45.0       # omega < 45 derajat/detik


class HysteresisFilter:
    """Menjaga stabilitas transisi status Tenang / Ragu-Ragu / Panik."""

    def __init__(self, config: Optional[HysteresisConfig] = None) -> None:
        self.config = config or HysteresisConfig()
        self.official_label: int = 0  # Mulai dari Tenang
        self._candidate_label: Optional[int] = None
        self._candidate_count: int = 0
        self._calm_streak_time: float = 0.0

    def reset(self) -> None:
        """Mengatur ulang filter ke status awal Tenang."""
        self.official_label = 0
        self._candidate_label = None
        self._candidate_count = 0
        self._calm_streak_time = 0.0

    def update(
        self,
        predicted_label: int,
        confidence: float,
        v: float,
        omega: float,
    ) -> Tuple[str, float]:
        """
        Memproses satu hasil inferensi window dan mengembalikan (state_resmi, confidence).

        Aturan:
        a. Koreksi salah deteksi: jika v < 0,5 dan omega < 45 terus-menerus selama 2,0 detik,
           status dikembalikan ke Tenang.
        b. Batas bertindak: status resmi hanya berubah jika confidence >= 0,80; jika di bawah
           itu, tahan status sebelumnya.
        c. Kestabilan: kelas prediksi baru harus sama selama minimal 3 window berturut-turut
           (1,5 detik) sebelum status resmi diperbarui.
        """
        # c. Koreksi salah deteksi (diam & pandangan stabil selama 2,0 detik)
        if v < self.config.calm_max_speed and omega < self.config.calm_max_jitter:
            self._calm_streak_time += self.config.window_duration
        else:
            self._calm_streak_time = 0.0

        if self._calm_streak_time >= self.config.calm_recovery_duration:
            self.official_label = 0
            self._candidate_label = None
            self._candidate_count = 0
            return LABEL_TO_STATE[self.official_label], confidence

        # a. Batas bertindak: confidence < 0,80 -> tahan status sebelumnya
        if confidence < self.config.confidence_threshold:
            self._candidate_label = None
            self._candidate_count = 0
            return LABEL_TO_STATE[self.official_label], confidence

        # b. Kestabilan: 3 window berturut-turut untuk kelas baru.
        # Jika berawal dari Tenang (0) dan terdeteksi sinyal Panik (2) dengan confidence >= 0.80,
        # pada window ke-1..2 naikkan bertahap ke Ragu-Ragu (1) terlebih dahulu sebelum mencapai
        # Panik (2) pada window ke-3 agar eskalasi Tenang -> Ragu-Ragu -> Panik selalu terasa.
        if predicted_label == self.official_label:
            self._candidate_label = None
            self._candidate_count = 0
        else:
            if predicted_label == self._candidate_label:
                self._candidate_count += 1
            else:
                self._candidate_label = predicted_label
                self._candidate_count = 1

            if (
                self.official_label == 0
                and self._candidate_label == 2
                and self._candidate_count < self.config.stability_windows
            ):
                self.official_label = 1
            elif (
                self.official_label == 0
                and self._candidate_label == 1
                and self._candidate_count >= 2
            ):
                self.official_label = 1
                self._candidate_label = None
                self._candidate_count = 0
            elif self._candidate_count >= self.config.stability_windows:
                self.official_label = self._candidate_label
                self._candidate_label = None
                self._candidate_count = 0

        return LABEL_TO_STATE[self.official_label], confidence

