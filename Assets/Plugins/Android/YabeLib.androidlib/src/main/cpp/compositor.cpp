#include <android/log.h>
#include <jni.h>
#include <sys/socket.h>
#include <unistd.h>
#include <yabe-compositor.h>

#include <atomic>
#include <string>
#include <thread>

#define LOG_TAG "YabeXreal"
#define LOG_DBG(...)                                                           \
  __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define LOG_ERR(...)                                                           \
  __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

namespace {
std::thread      compositor_thread;
std::atomic_bool compositor_running = false;
std::atomic_bool compositor_ready   = false;
JavaVM*          java_vm            = nullptr;
jobject          compositor_service = nullptr;
std::string      keymap_text;

class ScopedJniEnv {
 public:
  ScopedJniEnv() {
    if (!java_vm) {
      return;
    }
    const auto result =
        java_vm->GetEnv(reinterpret_cast<void**>(&env_), JNI_VERSION_1_6);
    if (result == JNI_OK) {
      return;
    }
    if (result != JNI_EDETACHED ||
        java_vm->AttachCurrentThread(&env_, nullptr) != JNI_OK) {
      env_ = nullptr;
      return;
    }
    attached_ = true;
  }

  ~ScopedJniEnv() {
    if (attached_) {
      java_vm->DetachCurrentThread();
    }
  }

  JNIEnv* get() const {
    return env_;
  }

 private:
  JNIEnv* env_      = nullptr;
  bool    attached_ = false;
};

int exec_in_client(const char* const * argv) {
  ScopedJniEnv scoped_env;
  auto*        env = scoped_env.get();
  if (!env || !compositor_service) {
    LOG_ERR("CompositorService is unavailable while executing a command");
    return -1;
  }

  auto* service_class = env->GetObjectClass(compositor_service);
  auto  exec_method =
      env->GetMethodID(service_class, "exec", "([Ljava/lang/String;)I");
  if (!exec_method) {
    env->ExceptionClear();
    env->DeleteLocalRef(service_class);
    LOG_ERR("Failed to find CompositorService.exec()");
    return -1;
  }

  size_t argc = 0;
  while (argv[argc]) {
    ++argc;
  }
  auto* string_class = env->FindClass("java/lang/String");
  auto* args         = env->NewObjectArray(argc, string_class, nullptr);
  for (size_t i = 0; i < argc; ++i) {
    auto* arg = env->NewStringUTF(argv[i]);
    env->SetObjectArrayElement(args, i, arg);
    env->DeleteLocalRef(arg);
  }
  const auto result = env->CallIntMethod(compositor_service, exec_method, args);
  env->DeleteLocalRef(args);
  env->DeleteLocalRef(string_class);
  env->DeleteLocalRef(service_class);

  if (env->ExceptionCheck()) {
    env->ExceptionDescribe();
    env->ExceptionClear();
    LOG_ERR("Failed to execute command in xreal-client");
    return -1;
  }
  return result;
}

const char* create_keymap(const char* rules, const char* model,
    const char* layout, const char* variant, const char* options) {
  keymap_text.clear();
  ScopedJniEnv scoped_env;
  auto*        env = scoped_env.get();
  if (!env || !compositor_service) {
    LOG_ERR("CompositorService is unavailable while creating a keymap");
    return nullptr;
  }

  auto* service_class        = env->GetObjectClass(compositor_service);
  auto  create_keymap_method = env->GetMethodID(service_class, "createKeymap",
      "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/"
      "String;Ljava/lang/String;)Ljava/lang/String;");
  if (!create_keymap_method) {
    env->ExceptionClear();
    env->DeleteLocalRef(service_class);
    LOG_ERR("Failed to find CompositorService.createKeymap()");
    return nullptr;
  }
  auto* rules_string   = env->NewStringUTF(rules);
  auto* model_string   = env->NewStringUTF(model);
  auto* layout_string  = env->NewStringUTF(layout);
  auto* variant_string = env->NewStringUTF(variant);
  auto* options_string = env->NewStringUTF(options);
  auto* result = static_cast<jstring>(env->CallObjectMethod(compositor_service,
      create_keymap_method, rules_string, model_string, layout_string,
      variant_string, options_string));
  env->DeleteLocalRef(rules_string);
  env->DeleteLocalRef(model_string);
  env->DeleteLocalRef(layout_string);
  env->DeleteLocalRef(variant_string);
  env->DeleteLocalRef(options_string);
  env->DeleteLocalRef(service_class);

  if (env->ExceptionCheck()) {
    env->ExceptionDescribe();
    env->ExceptionClear();
    LOG_ERR("Failed to request xkb keymap from xreal-client");
  } else if (result) {
    const char* result_chars = env->GetStringUTFChars(result, nullptr);
    if (result_chars) {
      keymap_text = result_chars;
      env->ReleaseStringUTFChars(result, result_chars);
    }
    env->DeleteLocalRef(result);
  }
  return keymap_text.empty() ? nullptr : keymap_text.c_str();
}
}  // namespace

extern "C" {
bool is_compositor_running() {
  return compositor_running.load();
}

void terminate_compositor() {
  if (!compositor_thread.joinable()) {
    LOG_DBG("compositor thread is not running");
    return;
  }
  if (compositor_running.load()) {
    LOG_DBG("terminating compositor thread");
    yabe_compositor_terminate();
  }
  compositor_thread.join();
  LOG_DBG("compositor thread terminated");
}

void send_keyboard_key(uint32_t key, bool is_pressed) {
  if (!compositor_ready.load()) {
    return;
  }
  yabe_compositor_key(key, is_pressed);
}

#define JNI_METHOD(return_type, name)                                          \
  extern "C" JNIEXPORT return_type JNICALL                                     \
      Java_org_yabe_xreal_CompositorService_##name

JNI_METHOD(void, startCompositor)(
    JNIEnv* env, jobject obj, jstring config_text) {
  if (compositor_running.exchange(true)) {
    LOG_DBG("compositor thread is already running");
    return;
  }
  if (compositor_thread.joinable()) {
    compositor_thread.join();
  }
  env->GetJavaVM(&java_vm);
  if (compositor_service) {
    env->DeleteGlobalRef(compositor_service);
  }
  compositor_service       = env->NewGlobalRef(obj);
  const char* config_chars = env->GetStringUTFChars(config_text, nullptr);
  std::string config(config_chars);
  env->ReleaseStringUTFChars(config_text, config_chars);
  compositor_thread = std::thread([config = std::move(config)]() {
    yabe_compositor_init(
        [](const char* s) {
          __android_log_print(ANDROID_LOG_DEBUG, "YabeCompositor", "%s", s);
        },
        create_keymap, exec_in_client);
    yabe_compositor_load_config(config.c_str());
    compositor_ready.store(true);
    LOG_DBG("compositor start");
    yabe_compositor_start();
    compositor_ready.store(false);
    compositor_running.store(false);
    LOG_DBG("finished: yabe_compositor_start()");
  });
}

JNI_METHOD(void, reloadConfig)(JNIEnv* env, jobject obj, jstring config_text) {
  if (!compositor_ready.load()) {
    LOG_DBG("compositor is not ready; config is not reloaded");
    return;
  }
  const char* config_chars = env->GetStringUTFChars(config_text, nullptr);
  yabe_compositor_load_config(config_chars);
  env->ReleaseStringUTFChars(config_text, config_chars);
  LOG_DBG("config reloaded");
}

JNI_METHOD(void, addClientFd)(JNIEnv* env, jobject obj, jint fd) {
  yabe_compositor_add_client_fd(fd);
}

JNI_METHOD(void, terminateCompositor)(JNIEnv* env, jobject obj) {
  terminate_compositor();
  if (compositor_service) {
    env->DeleteGlobalRef(compositor_service);
    compositor_service = nullptr;
  }
  keymap_text.clear();
}

JNI_METHOD(jboolean, isCompositorRunning)(JNIEnv* env, jobject obj) {
  return compositor_running.load();
}
}
