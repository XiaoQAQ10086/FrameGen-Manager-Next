# Copies the Rust core next to the Windows runner when it has been built.
# Invoked from windows/runner/CMakeLists.txt as a POST_BUILD step.

if(EXISTS "${FGM_SOURCE}")
  get_filename_component(fgm_dest_dir "${FGM_DEST}" DIRECTORY)
  file(COPY "${FGM_SOURCE}" DESTINATION "${fgm_dest_dir}")
else()
  message(WARNING
    "fgm_ffi.dll not found at ${FGM_SOURCE}; "
    "build rust/crates/fgm-ffi before packaging.")
endif()
