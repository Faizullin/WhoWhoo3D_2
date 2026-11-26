FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends \
    iptables libudev1 libgl1 libxcursor1 libxi6 libxrandr2 libxss1 libasound2 \
    && rm -rf /var/lib/apt/lists/*
