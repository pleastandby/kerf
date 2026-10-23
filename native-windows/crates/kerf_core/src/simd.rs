//! CPU SIMD feature detection and hardware capabilities.

/// Detects available SIMD features on x86_64.
pub fn detect_simd_capabilities() -> SimdFeatures {
    #[cfg(target_arch = "x86_64")]
    {
        SimdFeatures {
            has_sse41: is_x86_feature_detected!("sse4.1"),
            has_avx2: is_x86_feature_detected!("avx2"),
            has_fma: is_x86_feature_detected!("fma"),
        }
    }

    #[cfg(not(target_arch = "x86_64"))]
    {
        SimdFeatures {
            has_sse41: false,
            has_avx2: false,
            has_fma: false,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SimdFeatures {
    pub has_sse41: bool,
    pub has_avx2: bool,
    pub has_fma: bool,
}

impl SimdFeatures {
    pub fn description(&self) -> &'static str {
        if self.has_avx2 && self.has_fma {
            "AVX2 + FMA Accelerated"
        } else if self.has_avx2 {
            "AVX2 Accelerated"
        } else if self.has_sse41 {
            "SSE4.1 Accelerated"
        } else {
            "Standard Scalar Math"
        }
    }
}
