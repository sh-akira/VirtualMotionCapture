# hook to make a call to process rendered documents,
Jekyll::Hooks.register :site, :post_render do |site|
  hook_process(site)
end

def hook_process(site)
  site.collections.each do |_, collection|
    site.process_documents(collection.docs)
  end
  site.process_documents(site.pages)
end
