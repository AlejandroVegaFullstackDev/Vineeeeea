#!/usr/bin/env bash
# Compila ffmpeg y ffprobe (GPL, con x264, FreeType y HarfBuzz) como ejecutables para Android.
# Uso: build/android/build-ffmpeg.sh <arm64-v8a|x86_64>
#
# Los ejecutables se empaquetan en el APK como lib*.so dentro de jniLibs: Android los extrae a la
# carpeta nativa de la app y desde ahí sí se pueden ejecutar (a diferencia de la carpeta de datos).
set -euo pipefail

ABI=${1:?uso: $0 <arm64-v8a|x86_64>}
HERE=$(cd "$(dirname "$0")" && pwd)
# shellcheck source=../versions.env
source "$HERE/../versions.env"

API=26
NDK=${ANDROID_NDK_HOME:-$HOME/Android/Sdk/ndk/27.2.12479018}
TC=$NDK/toolchains/llvm/prebuilt/linux-x86_64
WORK=${WORK_DIR:-$HERE/work}
SRC=$WORK/src
BUILD=$WORK/build/$ABI
PREFIX=$WORK/prefix/$ABI
OUT=$HERE/out/$ABI
JOBS=${JOBS:-$(nproc)}

case "$ABI" in
  arm64-v8a) TRIPLE=aarch64-linux-android; FF_ARCH=aarch64; X264_ASM=(); FF_ASM=() ;;
  # Sin nasm el ensamblador x86 no está disponible: solo se usa para el emulador, no para teléfonos.
  x86_64)    TRIPLE=x86_64-linux-android;  FF_ARCH=x86_64;  X264_ASM=(--disable-asm); FF_ASM=(--disable-x86asm) ;;
  *) echo "ABI no soportada: $ABI" >&2; exit 1 ;;
esac

export CC="$TC/bin/${TRIPLE}${API}-clang"
export AR="$TC/bin/llvm-ar" RANLIB="$TC/bin/llvm-ranlib" STRIP="$TC/bin/llvm-strip" NM="$TC/bin/llvm-nm"
export PKG_CONFIG_LIBDIR="$PREFIX/lib/pkgconfig" PKG_CONFIG_PATH=
# Android 15+ pide páginas de 16 KB en todo binario nativo.
LDFLAGS_COMMON="-Wl,-z,max-page-size=16384"

mkdir -p "$SRC" "$BUILD" "$PREFIX" "$OUT"

fetch_git() { # nombre url ref
  [[ -d "$SRC/$1/.git" ]] || git clone -q "$2" "$SRC/$1"
  git -C "$SRC/$1" fetch -q --depth 1 origin "$3" 2>/dev/null || git -C "$SRC/$1" fetch -q origin
  git -C "$SRC/$1" checkout -q "$3" 2>/dev/null || git -C "$SRC/$1" checkout -q FETCH_HEAD
}

echo "== x264 ($X264_COMMIT) =="
fetch_git x264 https://code.videolan.org/videolan/x264.git "$X264_COMMIT"
mkdir -p "$BUILD/x264" && cd "$BUILD/x264"
"$SRC/x264/configure" --host="$TRIPLE" --cross-prefix="$TC/bin/llvm-" --prefix="$PREFIX" \
  --enable-static --enable-pic --disable-cli --disable-opencl "${X264_ASM[@]}" \
  --extra-ldflags="$LDFLAGS_COMMON"
make -j"$JOBS" && make install

echo "== FreeType $FREETYPE_VERSION =="
if [[ ! -d "$SRC/freetype-$FREETYPE_VERSION" ]]; then
  curl -sSL "https://download.savannah.gnu.org/releases/freetype/freetype-$FREETYPE_VERSION.tar.xz" | tar xJ -C "$SRC"
fi
mkdir -p "$BUILD/freetype" && cd "$BUILD/freetype"
"$SRC/freetype-$FREETYPE_VERSION/configure" --host="$TRIPLE" --prefix="$PREFIX" \
  --disable-shared --enable-static --with-pic \
  --with-zlib=no --with-bzip2=no --with-png=no --with-harfbuzz=no --with-brotli=no
make -j"$JOBS" && make install

# drawtext exige libfreetype Y libharfbuzz (da forma al texto); sin HarfBuzz el filtro no se compila.
echo "== HarfBuzz $HARFBUZZ_VERSION =="
if [[ ! -d "$SRC/harfbuzz-$HARFBUZZ_VERSION" ]]; then
  curl -sSL "https://github.com/harfbuzz/harfbuzz/releases/download/$HARFBUZZ_VERSION/harfbuzz-$HARFBUZZ_VERSION.tar.xz" | tar xJ -C "$SRC"
fi
rm -rf "$BUILD/harfbuzz" && mkdir -p "$BUILD/harfbuzz"
cmake -S "$SRC/harfbuzz-$HARFBUZZ_VERSION" -B "$BUILD/harfbuzz" -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$NDK/build/cmake/android.toolchain.cmake" -DANDROID_ABI="$ABI" -DANDROID_PLATFORM="android-$API" \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$PREFIX" -DCMAKE_PREFIX_PATH="$PREFIX" -DCMAKE_FIND_ROOT_PATH="$PREFIX" \
  -DBUILD_SHARED_LIBS=OFF -DCMAKE_POSITION_INDEPENDENT_CODE=ON -DHB_HAVE_FREETYPE=ON \
  -DHB_BUILD_UTILS=OFF -DHB_BUILD_SUBSET=OFF -DCMAKE_SHARED_LINKER_FLAGS="$LDFLAGS_COMMON"
cmake --build "$BUILD/harfbuzz" -j"$JOBS" && cmake --install "$BUILD/harfbuzz"

echo "== FFmpeg ($FFMPEG_TAG) =="
fetch_git ffmpeg https://github.com/FFmpeg/FFmpeg.git "$FFMPEG_TAG"
mkdir -p "$BUILD/ffmpeg" && cd "$BUILD/ffmpeg"
"$SRC/ffmpeg/configure" --prefix="$PREFIX" \
  --enable-cross-compile --target-os=android --arch="$FF_ARCH" \
  --cc="$CC" --ar="$AR" --ranlib="$RANLIB" --strip="$STRIP" --nm="$NM" \
  --pkg-config=pkg-config --pkg-config-flags=--static \
  --enable-gpl --enable-version3 --enable-libx264 --enable-libfreetype --enable-libharfbuzz \
  --enable-static --disable-shared --enable-pic \
  --disable-ffplay --disable-doc --disable-debug "${FF_ASM[@]}" \
  --extra-cflags="-I$PREFIX/include" \
  --extra-ldflags="-L$PREFIX/lib $LDFLAGS_COMMON" \
  --extra-libs="-lc++_static -lc++abi"  # HarfBuzz es C++
make -j"$JOBS"

# lib*.so: es el nombre que exige Android para extraer el archivo junto a las bibliotecas nativas.
cp ffmpeg  "$OUT/libffmpeg.so"
cp ffprobe "$OUT/libffprobe.so"
"$STRIP" "$OUT/libffmpeg.so" "$OUT/libffprobe.so"
echo "Listo: $OUT"; ls -la "$OUT"
