"use client"

import { useEffect, useState } from "react"

import { apiBlobRequest } from "@/lib/api-client"

export function useAuthenticatedImageUrl(imageUrl: string) {
  const [loadedImage, setLoadedImage] = useState({
    sourceUrl: "",
    objectUrl: "",
  })

  useEffect(() => {
    let isMounted = true
    let nextObjectUrl = ""

    if (!imageUrl) {
      return
    }

    async function loadImage() {
      try {
        const blob = await apiBlobRequest(imageUrl)
        if (!isMounted) {
          return
        }

        nextObjectUrl = URL.createObjectURL(blob)
        setLoadedImage({
          sourceUrl: imageUrl,
          objectUrl: nextObjectUrl,
        })
      } catch {
        if (isMounted) {
          setLoadedImage({
            sourceUrl: imageUrl,
            objectUrl: "",
          })
        }
      }
    }

    loadImage()

    return () => {
      isMounted = false
      if (nextObjectUrl) {
        URL.revokeObjectURL(nextObjectUrl)
      }
    }
  }, [imageUrl])

  return loadedImage.sourceUrl === imageUrl ? loadedImage.objectUrl : ""
}
