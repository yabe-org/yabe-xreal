cmake_minimum_required(VERSION 3.22)
project(compositor)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_COLOR_DIAGNOSTICS ON)
set(CMAKE_CXX_STANDARD 23)

file(
  GLOB_RECURSE sources
  ${CMAKE_CURRENT_LIST_DIR}/*.cpp
)
add_library(${PROJECT_NAME} SHARED ${sources})
target_link_libraries(${PROJECT_NAME}
  log
  GLESv3
  /home/watasuke/program/yabe/compositor/android-build/libyabe_compositor.a
)
target_include_directories(${PROJECT_NAME} PUBLIC
  /home/watasuke/program/yabe/compositor
)
