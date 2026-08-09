# 러다이트 2026 — SFX·BGM 절차 생성기 (GDD §12: 1순위 자체 생성 레트로 신스 — 터미널 미학)
# 팀 저작물: 이미지 생성 모델·외부 소스 미사용, 순수 수식 합성 (Placeholder 스프라이트 선례와 동일).
# 결정론: 난수는 고정 시드 → 재실행해도 같은 파일이 나온다.
# 실행: python generate_sfx.py  (출력: 상위 폴더 = Assets/_Project/Audio/)
import math
import os
import random
import struct
import wave

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
rng = random.Random(20260810)


def write_wav(name, samples):
    path = os.path.normpath(os.path.join(OUT, name + ".wav"))
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        frames = bytearray()
        for s in samples:
            s = max(-1.0, min(1.0, s))
            frames += struct.pack("<h", int(s * 32767))
        f.writeframes(bytes(frames))
    print(f"{name}.wav  {len(samples)/SR:.2f}s")


def seconds(n):
    return int(n * SR)


def square(phase):
    return 1.0 if (phase % 1.0) < 0.5 else -1.0


def saw(phase):
    return 2.0 * (phase % 1.0) - 1.0


def tri(phase):
    p = phase % 1.0
    return 4.0 * p - 1.0 if p < 0.5 else 3.0 - 4.0 * p


def render(duration, fn):
    n = seconds(duration)
    out = [0.0] * n
    phase = 0.0
    for i in range(n):
        t = i / SR
        sample, freq = fn(t, phase)
        phase += freq / SR
        out[i] = sample
    return out


def env_decay(t, duration, attack=0.005):
    if t < attack:
        return t / attack
    return max(0.0, 1.0 - (t - attack) / max(duration - attack, 1e-4))


def lowpass(samples, alpha):
    out = []
    prev = 0.0
    for s in samples:
        prev += alpha * (s - prev)
        out.append(prev)
    return out


def mix(*layers):
    n = max(len(l) for l in layers)
    out = [0.0] * n
    for l in layers:
        for i, s in enumerate(l):
            out[i] += s
    return out


# ── 1. PlayerShoot — 짧은 사각파 블립 (연사 5/s로 반복되므로 아주 짧고 절제) ──
def player_shoot():
    d = 0.07
    def fn(t, ph):
        f = 900.0 - 5500.0 * t          # 급강하 피치
        return square(ph) * 0.22 * env_decay(t, d), f
    return render(d, fn)


# ── 2. PlayerHit — 노이즈 타격 + 저음 둔탁 ──
def player_hit():
    d = 0.18
    noise = [ (rng.random() * 2 - 1) * 0.8 * env_decay(i / SR, d) for i in range(seconds(d)) ]
    noise = lowpass(noise, 0.25)
    def fn(t, ph):
        return math.sin(ph * 2 * math.pi) * 0.5 * env_decay(t, d), 110.0 - 200.0 * t
    return mix(noise, render(d, fn))


# ── 3. EnemyDeath — 하강 사각파 + 노이즈 붕괴 ──
def enemy_death():
    d = 0.24
    def fn(t, ph):
        f = 520.0 * (1.0 - t / d) + 90.0
        return square(ph) * 0.3 * env_decay(t, d), f
    tone = render(d, fn)
    noise = [ (rng.random() * 2 - 1) * 0.25 * env_decay(i / SR, d, 0.02) for i in range(seconds(d)) ]
    return mix(tone, lowpass(noise, 0.35))


# ── 4. AIAnalyze — 터미널 스캔 비프 3연 (패널 등장) ──
def ai_analyze():
    total = seconds(0.42)
    out = [0.0] * total
    for k, freq in enumerate((660.0, 880.0, 1174.0)):
        start = int(k * 0.13 * SR)
        for i in range(seconds(0.07)):
            t = i / SR
            if start + i < total:
                out[start + i] = math.sin(2 * math.pi * freq * t) * 0.3 * env_decay(t, 0.07)
    return out


# ── 5. PredictionShot — 상승 스윕 + 트레몰로 (마젠타 조준 경보 0.35s = 텔레그래프 길이) ──
def prediction_shot():
    d = 0.35
    def fn(t, ph):
        f = 420.0 + 900.0 * (t / d)
        trem = 0.6 + 0.4 * math.sin(2 * math.pi * 28.0 * t)
        return math.sin(ph * 2 * math.pi) * 0.3 * trem * env_decay(t, d, 0.02), f
    return render(d, fn)


# ── 6. PredictionFailed — 전용 글리치 (§12): 스터터 게이트 + 디튠 하강 + 비트크러시 ──
def prediction_failed():
    d = 0.5
    def fn(t, ph):
        f = 800.0 * (1.0 - t / d) + 180.0
        gate = 1.0 if (t * 25.0) % 1.0 < 0.55 else 0.15      # 25Hz 스터터
        detune = square(ph * 1.021)                            # 살짝 어긋난 2음 = 글리치 비팅
        s = (square(ph) + detune) * 0.5
        s = round(s * 6.0) / 6.0                               # 비트크러시
        return s * 0.42 * gate * env_decay(t, d, 0.01), f
    tone = render(d, fn)
    burst = [ (rng.random() * 2 - 1) * (0.3 if (i / SR * 25.0) % 1.0 < 0.3 else 0.0)
              * env_decay(i / SR, d) for i in range(seconds(d)) ]
    return mix(tone, burst)


# ── 7. WaveClear — 사각파 상승 아르페지오 (C5 E5 G5 C6) ──
def wave_clear():
    notes = (523.25, 659.25, 783.99, 1046.5)
    step = 0.1
    total = seconds(step * len(notes) + 0.15)
    out = [0.0] * total
    for k, freq in enumerate(notes):
        start = int(k * step * SR)
        dur = 0.22 if k == len(notes) - 1 else 0.11
        ph = 0.0
        for i in range(seconds(dur)):
            t = i / SR
            ph += freq / SR
            if start + i < total:
                out[start + i] += square(ph) * 0.2 * env_decay(t, dur, 0.004)
    return out


# ── 8. UiButton — 짧은 틱 ──
def ui_button():
    d = 0.045
    def fn(t, ph):
        return square(ph) * 0.18 * env_decay(t, d, 0.002), 1250.0
    return render(d, fn)


# ── 9. BossPhase — 저음 톱니 하강 + 느린 트레몰로 (PATTERN: YOU 전환의 불길함) ──
def boss_phase():
    d = 1.4
    def fn(t, ph):
        f = 220.0 * (1.0 - 0.7 * t / d)
        trem = 0.7 + 0.3 * math.sin(2 * math.pi * 6.0 * t)
        return saw(ph) * 0.35 * trem * env_decay(t, d, 0.05), f
    return lowpass(render(d, fn), 0.12)


# ── 10. BGM 전투 루프 — 100 BPM · 8마디 · Am Am F G (어두운 펄스 + 아르페지오, 이음매 없는 루프) ──
def bgm_loop():
    bpm = 100.0
    beat = 60.0 / bpm
    bars = 8
    total_time = bars * 4 * beat            # 19.2s
    n = seconds(total_time)
    roots = [110.0, 110.0, 87.31, 98.0]     # A2 A2 F2 G2 — 마디당 1코드, 2회 반복

    bass = [0.0] * n
    ph = 0.0
    for i in range(n):
        t = i / SR
        bar = int(t / (4 * beat)) % 4
        f = roots[bar]
        eighth = (t / (beat / 2)) % 1.0
        gate = env_decay(eighth * beat / 2, beat / 2, 0.004)   # 8분음 펄스
        ph += f / SR
        bass[i] = square(ph) * 0.16 * gate
    bass = lowpass(bass, 0.18)

    arp = [0.0] * n
    arp_notes = {110.0: (220.0, 261.63, 329.63),               # Am: A3 C4 E4
                 87.31: (174.61, 220.0, 261.63),               # F:  F3 A3 C4
                 98.0: (196.0, 246.94, 293.66)}                # G:  G3 B3 D4
    ph2 = 0.0
    for i in range(n):
        t = i / SR
        bar = int(t / (4 * beat)) % 4
        sixteenth = int(t / (beat / 4))
        notes = arp_notes[roots[bar]]
        f = notes[sixteenth % 3]
        pos = (t / (beat / 4)) % 1.0
        gate = env_decay(pos * beat / 4, beat / 4, 0.003)
        ph2 += f / SR
        arp[i] = tri(ph2) * 0.09 * gate

    hat = [0.0] * n
    for i in range(n):
        t = i / SR
        pos = (t / (beat / 2)) % 1.0
        if pos < 0.05:
            hat[i] = (rng.random() * 2 - 1) * 0.05 * (1.0 - pos / 0.05)

    return mix(bass, arp, hat)


if __name__ == "__main__":
    write_wav("Sfx_PlayerShoot", player_shoot())
    write_wav("Sfx_PlayerHit", player_hit())
    write_wav("Sfx_EnemyDeath", enemy_death())
    write_wav("Sfx_AiAnalyze", ai_analyze())
    write_wav("Sfx_PredictionShot", prediction_shot())
    write_wav("Sfx_PredictionFailed", prediction_failed())
    write_wav("Sfx_WaveClear", wave_clear())
    write_wav("Sfx_UiButton", ui_button())
    write_wav("Sfx_BossPhase", boss_phase())
    write_wav("Bgm_CombatLoop", bgm_loop())
    print("done -> Assets/_Project/Audio/")   # cp949 콘솔 안전을 위해 ASCII만
