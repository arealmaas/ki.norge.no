FROM denoland/deno:2.2.2

WORKDIR /app

# Copy dependency files first for layer caching
COPY deno.json deno.lock package.json ./

# Install dependencies
RUN deno install --allow-scripts=npm:sharp

# Copy source code
COPY . .

# Build the Astro site
RUN deno task build

# The Node adapter outputs a standalone server at dist/server/entry.mjs
ENV HOST=0.0.0.0
ENV PORT=4321
EXPOSE 4321

CMD ["deno", "-A", "dist/server/entry.mjs"]
