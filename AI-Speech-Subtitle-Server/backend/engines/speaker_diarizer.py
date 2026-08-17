import os
import io
import math
import numpy as np
from typing import List, Dict, Any, Optional, Tuple
import soundfile as sf
from scipy import signal
from scipy.fftpack import dct

# Bảng màu đại diện trực quan cho các nhân vật
SPEAKER_PALETTE = [
    {"color": "#3b82f6", "bg": "rgba(59, 130, 246, 0.18)", "border": "rgba(59, 130, 246, 0.4)"},  # Blue
    {"color": "#10b981", "bg": "rgba(16, 185, 129, 0.18)", "border": "rgba(16, 185, 129, 0.4)"},  # Emerald
    {"color": "#8b5cf6", "bg": "rgba(139, 92, 246, 0.18)", "border": "rgba(139, 92, 246, 0.4)"},  # Purple
    {"color": "#f59e0b", "bg": "rgba(245, 158, 11, 0.18)", "border": "rgba(245, 158, 11, 0.4)"},  # Amber
    {"color": "#ec4899", "bg": "rgba(236, 72, 153, 0.18)", "border": "rgba(236, 72, 153, 0.4)"},  # Pink
    {"color": "#06b6d4", "bg": "rgba(6, 182, 212, 0.18)", "border": "rgba(6, 182, 212, 0.4)"},   # Cyan
    {"color": "#ef4444", "bg": "rgba(239, 68, 68, 0.18)", "border": "rgba(239, 68, 68, 0.4)"},   # Red
    {"color": "#14b8a6", "bg": "rgba(20, 184, 166, 0.18)", "border": "rgba(20, 184, 166, 0.4)"},  # Teal
]

class SpeakerDiarizer:
    """
    Module Nhận diện giọng nói & Phân tách nhân vật (Online Voice Profile Bank & Matching).
    - Lưu lại hồ sơ âm giọng (Voice Profile) của từng người nói đã xuất hiện.
    - Duyệt qua từng câu thoại: Nếu khớp âm giọng đã lưu -> Xác định đúng người đó.
    - Nếu là giọng mới chưa từng có -> Tự động tạo nhân vật mới (SPEAKER_02, SPEAKER_03...).
    - Hoạt động 100% Offline, siêu nhanh, chuẩn xác cao.
    """
    def __init__(self, sample_rate: int = 16000):
        self.sample_rate = sample_rate

    def load_audio_segment(self, audio_path_or_bytes: Any, start: Optional[float] = None, end: Optional[float] = None) -> np.ndarray:
        """Đọc và chuyển đổi audio thành mảng 1D numpy float32 tại tần số 16kHz"""
        try:
            if isinstance(audio_path_or_bytes, (str, os.PathLike)):
                if not os.path.exists(str(audio_path_or_bytes)):
                    return np.zeros(self.sample_rate, dtype=np.float32)
                import av
                container = av.open(str(audio_path_or_bytes))
                stream = container.streams.audio[0]
                resampler = av.AudioResampler(format='fltp', layout='mono', rate=self.sample_rate)

                samples = []
                for frame in container.decode(stream):
                    resampled = resampler.resample(frame)
                    if resampled:
                        for r_frame in resampled:
                            samples.append(r_frame.to_ndarray()[0])

                container.close()
                if len(samples) == 0:
                    return np.zeros(self.sample_rate, dtype=np.float32)
                full_audio = np.concatenate(samples).astype(np.float32)

                if start is not None or end is not None:
                    st_idx = int(max(0, (start or 0.0) * self.sample_rate))
                    en_idx = int(min(len(full_audio), (end or len(full_audio)/self.sample_rate) * self.sample_rate))
                    if en_idx > st_idx:
                        return full_audio[st_idx:en_idx]
                return full_audio

            elif isinstance(audio_path_or_bytes, (bytes, io.BytesIO)):
                bio = audio_path_or_bytes if isinstance(audio_path_or_bytes, io.BytesIO) else io.BytesIO(audio_path_or_bytes)
                data, sr = sf.read(bio, dtype='float32')
                if data.ndim > 1:
                    data = np.mean(data, axis=1)
                if sr != self.sample_rate:
                    num_samples = int(len(data) * self.sample_rate / sr)
                    data = signal.resample(data, num_samples)
                return data.astype(np.float32)

            elif isinstance(audio_path_or_bytes, np.ndarray):
                arr = audio_path_or_bytes
                if arr.ndim > 1:
                    arr = np.mean(arr, axis=1)
                return arr.astype(np.float32)

            else:
                return np.zeros(self.sample_rate, dtype=np.float32)

        except Exception as e:
            print(f"[SpeakerDiarizer] Canh bao khi doc audio: {e}")
            return np.zeros(self.sample_rate, dtype=np.float32)

    def extract_voice_embedding(self, audio: np.ndarray) -> np.ndarray:
        """
        Trích xuất vector đặc trưng âm sắc phân biệt cao độ thanh quản và hình dạng vòm họng.
        Phân biệt rõ ràng cao độ giọng nam, nữ, trẻ em và âm sắc từng người nói.
        """
        if len(audio) < 400:
            return np.zeros(17, dtype=np.float32)

        # 1. Khử DC và chuẩn hóa năng lượng
        audio = audio - np.mean(audio)
        max_val = np.max(np.abs(audio))
        if max_val > 1e-5:
            norm_audio = audio / max_val
        else:
            norm_audio = audio

        # 2. Ước lượng cao độ thanh quản (Pitch F0 via Autocorrelation)
        corr = np.correlate(norm_audio, norm_audio, mode='full')[len(norm_audio)-1:]
        min_lag = int(self.sample_rate / 400) # 400Hz (giọng nữ/trẻ em cao)
        max_lag = int(self.sample_rate / 65)  # 65Hz (giọng nam rất trầm)
        if max_lag > len(corr):
            max_lag = len(corr) - 1

        if max_lag > min_lag:
            pitch_lag = min_lag + np.argmax(corr[min_lag:max_lag])
            pitch_f0 = float(self.sample_rate / pitch_lag) if pitch_lag > 0 else 150.0
        else:
            pitch_f0 = 150.0

        # 3. STFT & Mel-Frequency Filterbank
        n_fft, hop_length, win_length, n_mels = 512, 160, 400, 20
        window = signal.windows.hamming(win_length)
        
        stft_matrix = []
        for i in range(0, len(norm_audio) - win_length, hop_length):
            frame = norm_audio[i : i + win_length] * window
            spec = np.abs(np.fft.rfft(frame, n=n_fft))
            stft_matrix.append(spec)

        if len(stft_matrix) == 0:
            return np.zeros(17, dtype=np.float32)

        stft_matrix = np.array(stft_matrix) # (frames, freq_bins)

        # Mel Filterbank
        low_freq, high_freq = 80.0, self.sample_rate / 2.0
        low_mel = 2595.0 * np.log10(1.0 + low_freq / 700.0)
        high_mel = 2595.0 * np.log10(1.0 + high_freq / 700.0)
        mel_points = np.linspace(low_mel, high_mel, n_mels + 2)
        hz_points = 700.0 * (10.0 ** (mel_points / 2595.0) - 1.0)
        bin_points = np.floor((n_fft + 1) * hz_points / self.sample_rate).astype(int)

        fbank = np.zeros((n_mels, int(n_fft / 2 + 1)))
        for m in range(1, n_mels + 1):
            f_m_minus = bin_points[m - 1]
            f_m = bin_points[m]
            f_m_plus = bin_points[m + 1]

            for k in range(f_m_minus, f_m):
                if f_m - f_m_minus > 0:
                    fbank[m - 1, k] = (k - bin_points[m - 1]) / (f_m - f_m_minus + 1e-5)
            for k in range(f_m, f_m_plus):
                if f_m_plus - f_m > 0:
                    fbank[m - 1, k] = (bin_points[m + 1] - k) / (f_m_plus - f_m + 1e-5)

        mel_spec = np.dot(stft_matrix, fbank.T)
        log_mel = np.log(np.maximum(mel_spec, 1e-5))

        # 4. Trích xuất 13 hệ số MFCC qua DCT-II
        mfcc = dct(log_mel, type=2, axis=-1, norm='ortho')[:, 1:14] # (T, 13)

        # 5. CMVN (Cepstral Mean Subtraction) để chỉ lấy âm sắc thuần túy
        mfcc_cms = mfcc - np.mean(mfcc, axis=0, keepdims=True)
        mfcc_std = np.std(mfcc_cms, axis=0) # 13

        # 6. Chuẩn hóa cao độ & Phổ âm sắc với độ nhạy cao
        freqs = np.fft.rfftfreq(n_fft, d=1.0/self.sample_rate)
        centroids = np.sum(stft_matrix * freqs, axis=1) / (np.sum(stft_matrix, axis=1) + 1e-5)
        
        pitch_scaled = (pitch_f0 - 160.0) / 60.0 # Thang đo cao độ thanh quản
        centroid_scaled = (np.mean(centroids) - 1800.0) / 600.0 # Thang đo độ sáng giọng
        spectral_spread = (np.std(centroids) - 1000.0) / 400.0
        energy_skew = float(np.mean(((stft_matrix - np.mean(stft_matrix)) / (np.std(stft_matrix) + 1e-5))**3)) / 5.0

        vec = np.concatenate([
            mfcc_std, # 13
            [pitch_scaled * 2.5, centroid_scaled * 1.5, spectral_spread, energy_skew] # 4
        ])

        norm = np.linalg.norm(vec)
        return (vec / norm if norm > 1e-6 else vec).astype(np.float32)

    def cosine_similarity(self, vec_a: np.ndarray, vec_b: np.ndarray) -> float:
        """Tính độ tương đồng Cosine giữa 2 vector [-1.0 đến 1.0]"""
        norm_a = np.linalg.norm(vec_a)
        norm_b = np.linalg.norm(vec_b)
        if norm_a < 1e-6 or norm_b < 1e-6:
            return 0.0
        return float(np.dot(vec_a, vec_b) / (norm_a * norm_b))

    def diarize_and_identify(
        self,
        audio_path: str,
        segments: List[Dict[str, Any]],
        character_samples: Optional[List[Dict[str, Any]]] = None,
        num_speakers: Optional[int] = None,
        similarity_threshold: float = 0.92
    ) -> List[Dict[str, Any]]:
        """
        Phân tách nhân vật & Nhận diện giọng nói tuần tự (Online Voice Profile Bank).
        - Duyệt từng câu thoại: So khớp với kho hồ sơ giọng nói đã lưu.
        - Khớp giọng -> Xác định đúng người đó & cập nhật hồ sơ giọng.
        - Giọng mới -> Tạo nhãn nhân vật mới (SPEAKER_02, SPEAKER_03...).
        """
        if not segments:
            return segments

        # 1. Nạp toàn bộ file audio vào bộ nhớ để cắt lát cực nhanh
        full_audio = None
        if audio_path and os.path.exists(audio_path):
            full_audio = self.load_audio_segment(audio_path)

        # 2. Khởi tạo Kho Hồ Sơ Giọng Nói (Voice Profile Bank)
        known_profiles = []

        # Nạp trước các mẫu nhân vật do người dùng cung cấp (nếu có)
        if character_samples:
            for char_item in character_samples:
                name = char_item.get("name") or char_item.get("speaker") or "Nhân vật"
                c_path = char_item.get("audio_path")
                c_b64 = char_item.get("audio_base64")
                c_color = char_item.get("color")

                sample_audio = None
                if c_path and os.path.exists(c_path):
                    sample_audio = self.load_audio_segment(c_path)
                elif c_b64:
                    import base64
                    try:
                        raw_bytes = base64.b64decode(c_b64)
                        sample_audio = self.load_audio_segment(raw_bytes)
                    except Exception as e:
                        print(f"[SpeakerDiarizer] Loi doc base64 mau giong: {e}")

                if sample_audio is not None and len(sample_audio) > 1000:
                    feat = self.extract_voice_embedding(sample_audio)
                    pal_idx = len(known_profiles) % len(SPEAKER_PALETTE)
                    known_profiles.append({
                        "name": name,
                        "center": feat,
                        "count": 1,
                        "color": c_color or SPEAKER_PALETTE[pal_idx]["color"],
                        "is_fixed_reference": True
                    })
                    print(f"[SpeakerDiarizer] [OK] Da nap mau giong nhan vat '{name}'")

        # 3. Duyệt tuần tự qua từng câu thoại trong Timeline và so khớp hồ sơ giọng nói
        for idx, seg in enumerate(segments):
            st = float(seg.get("start", 0.0))
            en = float(seg.get("end", st + 1.0))
            if en - st < 0.2:
                en = st + 0.3

            if full_audio is not None and len(full_audio) > 0:
                st_idx = int(max(0, st * self.sample_rate))
                en_idx = int(min(len(full_audio), en * self.sample_rate))
                if en_idx > st_idx:
                    seg_audio = full_audio[st_idx:en_idx]
                else:
                    seg_audio = np.zeros(self.sample_rate, dtype=np.float32)
            else:
                seg_audio = self.load_audio_segment(audio_path, start=st, end=en)

            feat = self.extract_voice_embedding(seg_audio)

            best_profile = None
            best_similarity = -1.0

            for prof in known_profiles:
                sim = self.cosine_similarity(feat, prof["center"])
                if sim > best_similarity:
                    best_similarity = sim
                    best_profile = prof

            # Kiểm tra nếu khớp với hồ sơ giọng của người đã từng nói trước đó
            if best_profile and best_similarity >= similarity_threshold:
                # Cập nhật vector đại diện (Centroid) để thích nghi với ngữ điệu của người này
                if not best_profile.get("is_fixed_reference", False):
                    cnt = best_profile["count"]
                    new_center = (best_profile["center"] * cnt + feat) / (cnt + 1)
                    c_norm = np.linalg.norm(new_center)
                    best_profile["center"] = (new_center / c_norm).astype(np.float32)
                    best_profile["count"] += 1

                seg["speaker"] = best_profile["name"]
                seg["speaker_id"] = best_profile["name"]
                seg["speaker_confidence"] = round(best_similarity, 2)
                seg["speaker_color"] = best_profile["color"]
            else:
                # Giọng nói hoàn toàn mới -> Tạo hồ sơ nhân vật mới!
                next_num = len(known_profiles) + 1
                new_spk_name = f"SPEAKER_{next_num:02d}"
                pal_idx = (next_num - 1) % len(SPEAKER_PALETTE)
                spk_color = SPEAKER_PALETTE[pal_idx]["color"]

                known_profiles.append({
                    "name": new_spk_name,
                    "center": np.array(feat, dtype=np.float32),
                    "count": 1,
                    "color": spk_color,
                    "is_fixed_reference": False
                })

                seg["speaker"] = new_spk_name
                seg["speaker_id"] = new_spk_name
                seg["speaker_confidence"] = 1.0
                seg["speaker_color"] = spk_color

        # 4. Nếu người dùng chọn số lượng nhân vật ước lượng (num_speakers) và phát hiện nhiều hơn:
        # Tự động gộp 2 nhóm có giọng gần nhau nhất về 1 người
        if num_speakers and int(num_speakers) >= 1:
            target_k = int(num_speakers)
            while len(known_profiles) > target_k:
                # Tìm cặp profile gần nhau nhất
                closest_i, closest_j = 0, 1
                max_pair_sim = -1.0
                for i in range(len(known_profiles)):
                    for j in range(i + 1, len(known_profiles)):
                        sim_pair = self.cosine_similarity(known_profiles[i]["center"], known_profiles[j]["center"])
                        if sim_pair > max_pair_sim:
                            max_pair_sim = sim_pair
                            closest_i, closest_j = i, j

                merged_name = known_profiles[closest_i]["name"]
                merged_color = known_profiles[closest_i]["color"]
                abandon_name = known_profiles[closest_j]["name"]

                # Cập nhật lại các câu thoại có tên abandon_name thành merged_name
                for seg in segments:
                    if seg.get("speaker") == abandon_name:
                        seg["speaker"] = merged_name
                        seg["speaker_id"] = merged_name
                        seg["speaker_color"] = merged_color

                known_profiles.pop(closest_j)

        return segments

# Global Singleton Instance
_global_diarizer = None

def get_global_diarizer() -> SpeakerDiarizer:
    global _global_diarizer
    if _global_diarizer is None:
        _global_diarizer = SpeakerDiarizer(sample_rate=16000)
    return _global_diarizer
