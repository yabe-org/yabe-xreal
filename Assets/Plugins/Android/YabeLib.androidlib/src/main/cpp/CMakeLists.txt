cmake_minimum_required(VERSION 3.22)
project(compositor)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_COLOR_DIAGNOSTICS ON)
set(CMAKE_CXX_STANDARD 23)

configure_file(meson_cross.txt.in ${CMAKE_CURRENT_BINARY_DIR}/meson_cross.txt)
set(COMPOSITOR_SUBMODULE_DIR ${CMAKE_CURRENT_LIST_DIR}/../../../yabe-compositor)
include(ExternalProject)
ExternalProject_Add(
    yabe_compositor_project
    SOURCE_DIR ${COMPOSITOR_SUBMODULE_DIR}
    BUILD_ALWAYS TRUE
    CONFIGURE_COMMAND meson setup <BINARY_DIR> <SOURCE_DIR> --cross-file ${CMAKE_CURRENT_BINARY_DIR}/meson_cross.txt
    BUILD_COMMAND meson compile -C <BINARY_DIR>
    INSTALL_COMMAND ""
    BUILD_BYPRODUCTS <BINARY_DIR>/libyabe_compositor.a
)
ExternalProject_Get_Property(yabe_compositor_project BINARY_DIR)
set(MESON_BUILD_DIR ${BINARY_DIR})
add_library(yabe_compositor STATIC IMPORTED)
set_target_properties(yabe_compositor PROPERTIES
    IMPORTED_LOCATION ${MESON_BUILD_DIR}/libyabe_compositor.a
)
add_library(${PROJECT_NAME} SHARED compositor.cpp)
add_dependencies(${PROJECT_NAME}
  yabe_compositor_project
)
target_link_libraries(${PROJECT_NAME}
  log
  GLESv3
  yabe_compositor
)
target_include_directories(${PROJECT_NAME} PUBLIC
  ${CMAKE_CURRENT_BINARY_DIR}
  ${COMPOSITOR_SUBMODULE_DIR}
)
