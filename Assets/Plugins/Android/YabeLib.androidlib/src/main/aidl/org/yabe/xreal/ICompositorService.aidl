package org.yabe.xreal;
import android.os.ParcelFileDescriptor;

interface ICompositorService {
  void registerSocket(in ParcelFileDescriptor pfd);
}
