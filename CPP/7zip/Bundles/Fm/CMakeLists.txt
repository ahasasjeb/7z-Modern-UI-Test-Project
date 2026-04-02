cmake_minimum_required(VERSION 3.20)
project(7zFM C CXX ASM)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)

set(CMAKE_CXX_FLAGS_INIT "/DZ7_LANG /DZ7_DEVICE_FILE /Gr /Wall /WX /EHsc /Gy /GF /MT /GS- /Zc:wchar_t /Zc:forScope /Zc:throwingNew /MP16 /O1")
set(CMAKE_C_FLAGS_INIT "/DZ7_LANG /DZ7_DEVICE_FILE /Gr /Wall /WX /EHsc /Gy /GF /MT /GS- /Zc:wchar_t /Zc:forScope /MP16 /O1")

if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    set(CMAKE_CXX_FLAGS_INIT "${CMAKE_CXX_FLAGS_INIT} /MTd /Od /Zi")
    set(CMAKE_C_FLAGS_INIT "${CMAKE_C_FLAGS_INIT} /MTd /Od /Zi")
else()
    set(CMAKE_CXX_FLAGS_INIT "${CMAKE_CXX_FLAGS_INIT} /MT /O1 /GL")
    set(CMAKE_C_FLAGS_INIT "${CMAKE_C_FLAGS_INIT} /MT /O1 /GL")
endif()

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    add_compile_definitions(_WIN64 WIN64)
    set(OBJ_DIR "x64")
    enable_language(ASM_MASM)
else()
    set(OBJ_DIR "")
endif()

file(GLOB_RECURSE COMMON_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../Common/*.cpp"
)
file(GLOB_RECURSE WINDOWS_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../Windows/*.cpp"
)
file(GLOB_RECURSE WIN_CTRL_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../Windows/Control/*.cpp"
)
file(GLOB_RECURSE UI_COMMON_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Common/*.cpp"
)
file(GLOB_RECURSE FM_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/FileManager/*.cpp"
)
file(GLOB_RECURSE GUI_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/GUI/*.cpp"
)
file(GLOB_RECURSE EXPLORER_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Explorer/*.cpp"
)
file(GLOB_RECURSE AGENT_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Agent/*.cpp"
)

list(FILTER COMMON_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER WINDOWS_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER WIN_CTRL_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER UI_COMMON_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER FM_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER GUI_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER EXPLORER_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")
list(FILTER AGENT_SOURCES EXCLUDE REGEX ".*/StdAfx\\.cpp$")

file(GLOB FM_RC_SOURCES 
    "${CMAKE_CURRENT_SOURCE_DIR}/resource.rc"
)

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    file(GLOB ASM_SOURCES 
        "${CMAKE_CURRENT_SOURCE_DIR}/../../../../Asm/x86/*.asm"
    )
endif()

add_executable(7zFM
    ${FM_SOURCES}
    ${GUI_SOURCES}
    ${EXPLORER_SOURCES}
    ${AGENT_SOURCES}
    ${UI_COMMON_SOURCES}
    ${COMMON_SOURCES}
    ${WINDOWS_SOURCES}
    ${WIN_CTRL_SOURCES}
    ${FM_RC_SOURCES}
    StdAfx.cpp
)

if(CMAKE_SIZEOF_VOID_P EQUAL 8 AND ASM_SOURCES)
    set_target_properties(7zFM PROPERTIES
        LINKER_LANGUAGE ASM_MASM
    )
    set_source_files_properties(${ASM_SOURCES} PROPERTIES 
        COMPILE_FLAGS "/c /fox64"
        OBJECT_OUTPUTS "${OBJ_DIR}"
    )
    target_sources(7zFM PRIVATE ${ASM_SOURCES})
endif()

target_include_directories(7zFM PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../..
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../Common
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../Windows
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../Windows/Control
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Common
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/FileManager
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/GUI
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Explorer
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../UI/Agent
    ${CMAKE_CURRENT_SOURCE_DIR}/../../..
    ${CMAKE_CURRENT_SOURCE_DIR}/../../../../Asm/x86
)

if(CMAKE_CXX_COMPILER_ID MATCHES "MSVC")
    if(CMAKE_SIZEOF_VOID_P EQUAL 8)
        target_compile_options(7zFM PRIVATE /favor:AMD64)
    endif()
endif()

set_target_properties(7zFM PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_CURRENT_SOURCE_DIR}/${OBJ_DIR}
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_CURRENT_SOURCE_DIR}/${OBJ_DIR}
    PDB_OUTPUT_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/${OBJ_DIR}
)
