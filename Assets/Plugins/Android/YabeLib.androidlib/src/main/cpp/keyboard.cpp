#include <android/log.h>
#include <linux/memfd.h>
#include <sys/mman.h>
#include <sys/syscall.h>
#include <unistd.h>
#include <xkbcommon/xkbcommon.h>
#include <yabe-compositor.h>

#include <cstdlib>
#include <cstring>

#include "default-keymap.hpp"

#define LOG_TAG "YabeXreal"
#define LOG_ERR(...)                                                           \
  __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

bool compositor_is_initialized();

namespace {
xkb_state*         xkb_state_      = nullptr;
xkb_mod_mask_t     prev_depressed_ = 0;
xkb_mod_mask_t     prev_latched_   = 0;
xkb_mod_mask_t     prev_locked_    = 0;
xkb_layout_index_t prev_group_     = 0;
}

extern "C" {
bool initialize_keyboard() {
  if (!compositor_is_initialized()) {
    return false;
  }
  if (xkb_state_) {
    return true;
  }

  auto* context = xkb_context_new(XKB_CONTEXT_NO_DEFAULT_INCLUDES);
  if (!context) {
    LOG_ERR("Failed to create xkb context");
    return false;
  }
  auto* keymap = xkb_keymap_new_from_string(context, kDefaultKeymap,
      XKB_KEYMAP_FORMAT_TEXT_V1, XKB_KEYMAP_COMPILE_NO_FLAGS);
  xkb_context_unref(context);
  if (!keymap) {
    LOG_ERR("Failed to create xkb keymap");
    return false;
  }

  auto* state = xkb_state_new(keymap);
  char* keymap_str =
      xkb_keymap_get_as_string(keymap, XKB_KEYMAP_FORMAT_TEXT_V1);
  xkb_keymap_unref(keymap);
  if (!state || !keymap_str) {
    LOG_ERR("Failed to create xkb state or serialize its keymap");
    if (state) {
      xkb_state_unref(state);
    }
    free(keymap_str);
    return false;
  }

  const auto size = static_cast<uint32_t>(std::strlen(keymap_str) + 1);
  const int  fd   = static_cast<int>(
      syscall(SYS_memfd_create, "yabe-xkb-keymap", MFD_CLOEXEC));
  if (fd < 0 || ftruncate(fd, size) < 0) {
    LOG_ERR("Failed to create xkb keymap shared memory");
    if (fd >= 0) {
      close(fd);
    }
    free(keymap_str);
    xkb_state_unref(state);
    return false;
  }
  void* ptr = mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
  if (ptr == MAP_FAILED) {
    LOG_ERR("Failed to map xkb keymap shared memory");
    close(fd);
    free(keymap_str);
    xkb_state_unref(state);
    return false;
  }
  std::memcpy(ptr, keymap_str, size);
  munmap(ptr, size);
  free(keymap_str);

  yabe_compositor_keymap(1, fd, size);
  close(fd);
  yabe_compositor_repeat_info(33, 500);
  xkb_state_      = state;
  prev_depressed_ = 0;
  prev_latched_   = 0;
  prev_locked_    = 0;
  prev_group_     = 0;
  return true;
}

void destroy_keyboard() {
  if (!xkb_state_) {
    return;
  }
  xkb_state_unref(xkb_state_);
  xkb_state_ = nullptr;
}

void send_keyboard_key(uint32_t key, bool is_pressed) {
  if (!xkb_state_) {
    return;
  }
  yabe_compositor_key(key, is_pressed);
  xkb_state_update_key(
      xkb_state_, key + 8, is_pressed ? XKB_KEY_DOWN : XKB_KEY_UP);

  const auto depressed =
      xkb_state_serialize_mods(xkb_state_, XKB_STATE_MODS_DEPRESSED);
  const auto latched =
      xkb_state_serialize_mods(xkb_state_, XKB_STATE_MODS_LATCHED);
  const auto locked =
      xkb_state_serialize_mods(xkb_state_, XKB_STATE_MODS_LOCKED);
  const auto group =
      xkb_state_serialize_layout(xkb_state_, XKB_STATE_LAYOUT_EFFECTIVE);
  if (depressed == prev_depressed_ && latched == prev_latched_ &&
      locked == prev_locked_ && group == prev_group_) {
    return;
  }
  yabe_compositor_modifiers(depressed, latched, locked, group);
  prev_depressed_ = depressed;
  prev_latched_   = latched;
  prev_locked_    = locked;
  prev_group_     = group;
}
}
